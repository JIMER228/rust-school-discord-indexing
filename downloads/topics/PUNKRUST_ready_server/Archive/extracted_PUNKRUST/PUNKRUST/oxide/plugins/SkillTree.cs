using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using WebSocketSharp;

/* Ideas
 * - Create a buff for comfort that uses the same method as HealthRegen.
 * Vehiclular combatant - more damage to vehicles (maybe including bradley and heli?)
 * Raiding skill tree.
 * Perk Idea - vehicle salvaging. Remove modules from vehicles.
 * Add heli as XP source.
 * Make leaderboard information a permanent thing, with the option to wipe it, so it always displays what the highest score was from prior wipes.
 * Vehicle speeds (helicopter, car)
 * Add CUI option to enable/disable components that are shredded by the shredder perk.
 */

/* Changed 1.2.9
 * Changed the xp loss on death options an added suicide option.
 * Added support for EventHelper to prevent xp loss on death.
 * Added whitespace trimming to permission checks.
 * Added additional null checks to CanLootEntity.
 * Fixed the locatenode command for the mining ultimate.
 */

namespace Oxide.Plugins
{
    [Info("Skill Tree", "imthenewguy", "1.2.9")]
    [Description("Skills on a tree!")]
    class SkillTree : RustPlugin
    {
        #region Config

        private Configuration config;
        public class Configuration
        {
            [JsonProperty("General settings")]
            public GeneralSettings general_settings = new GeneralSettings();

            public class GeneralSettings
            {
                [JsonProperty("Points required to unlock Tier 2 nodes in the tech trees")]
                public int t2_points_required = 5;

                [JsonProperty("Points required to unlock Tier 3 nodes in the tech trees")]
                public int t3_points_required = 10;

                [JsonProperty("Points required to unlock the Ultimate node in tech trees")]
                public int ultimate_points_required = 25;

                [JsonProperty("Skill points per level")]
                public int points_per_level = 2;

                [JsonProperty("Maximum points a player can spend [default]")]
                public int max_skill_points = 200;

                [JsonProperty("Modified max skill points based on permissions [must be higher than default]")]
                public Dictionary<string, int> max_skill_points_override = new Dictionary<string, int>();

                [JsonProperty("Maximum level a player can get to")]
                public int max_player_level = 100;

                [JsonProperty("Allow players to respec their skill points")]
                public bool allow_respecs = true;

                [JsonProperty("Cost per point to respec [default]")]
                public double respec_cost = 30;

                [JsonProperty("Cost per point to respec based on permissions [must be lower than default]")]
                public Dictionary<string, double> respec_cost_override = new Dictionary<string, double>();

                [JsonProperty("Currency type to respec [scrap, economics, srp, custom]")]
                public string respec_currency = "scrap";

                [JsonProperty("If currency is set to custom, what are the details of the item")]
                public CustomCurrency respec_currency_custom = new CustomCurrency();

                [JsonProperty("Multiplier increase after each respec [0.2 = a 20% increase in the cost to respec each time. 0 = no increase] [resets on wipe or data reset]")]
                public float respec_multiplier = 0;

                [JsonProperty("Maximum value that the respec multiplier can get to [0 = no limit]")]
                public float respec_multiplier_max = 0;

                [JsonProperty("List of rewards the player receives based on level")]
                public Dictionary<int, LevelReward> level_rewards = new Dictionary<int, LevelReward>();

                [JsonProperty("Require players to have specific tree permissions to open them")]
                public bool require_tree_perms = false;

                [JsonProperty("Drop bag on death")]
                public bool drop_bag_on_death = true;

                [JsonProperty("XP pump bar settings")]
                public PumpBar pump_bar_settings = new PumpBar();

                [JsonProperty("Cache images using skinid or url? [skinid, url]")]
                public string image_cache_source = "url";

                [JsonProperty("Redownload all images when the plugin reloads if using URL?")]
                public bool replace_on_reload = false;
            }

            [JsonProperty("Buff settings")]
            public BuffSettings buff_settings = new BuffSettings();

            public class BuffSettings
            {
                [JsonProperty("Minimum components for the component perk")]
                public int min_components = 1;

                [JsonProperty("Maximum components for the component perk")]
                public int max_components = 1;

                [JsonProperty("Minimum electrical components for the electrical component perk")]
                public int min_electrical_components = 1;

                [JsonProperty("Maximum electrical components for the electrical component perk")]
                public int max_electrical_components = 1;

                [JsonProperty("Minimum additional scrap to be added to crates and barrels")]
                public int min_extra_scrap = 1;

                [JsonProperty("Maximum additional scrap to be added to crates and barrels")]
                public int max_extra_scrap = 2;

                [JsonProperty("PVP Buff Critical damage modifier. Picks a random value between 0 and the declared value, and += the damage% onto the hit")]
                public float pvp_critical_modifier = 0.3f;

                [JsonProperty("Should the LootPickup buff only work with melee weapons?")]
                public bool lootPickupBuffMeleeOnly = false;

                [JsonProperty("Should we have a maximum distance for the loot pickup buff [0 = unlimited]?")]
                public float loot_pickup_buff_max_distance = 0;

                [JsonProperty("List of animals for animal resist ability", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<string> animals = new List<string>();

                [JsonProperty("Details of a horse to change when a player mounts a horse with the relevant perk unlocked")]
                public HorseStats horse_buff_info = new HorseStats();

                [JsonProperty("Delay for HealthRegen perk after taking damage")]
                public float health_regen_combat_delay = 5f;

                [JsonProperty("Delay between attempts at tracking animals")]
                public float track_delay = 10f;

                [JsonProperty("List of skins that the rationer perk will not refund")]
                public List<ulong> no_refund_item_skins = new List<ulong>();

                [JsonProperty("Bag cooldown time")]
                public float bag_cooldown_time = 10f;

                [JsonProperty("Bag prefab")]
                public string bag_prefab = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";

                [JsonProperty("Primitive weapons for the primitive weapons ability", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<string> primitive_weapons = new List<string>();

                [JsonProperty("Harvesting yield blacklist [items listed here will not be affected by the harvesting yield perks]")]
                public List<string> harvest_yield_blacklist = new List<string>();
            }

            [JsonProperty("Chat command settings")]
            public ChatCommands chat_commands = new ChatCommands();

            public class ChatCommands
            {
                [JsonProperty("Use mouse 3 to toggle the boat turbo (performance heavy on high pop servers). Set false to use a chat command instead")]
                public bool use_input_key_boat = false;

                [JsonProperty("Chat command for turbo")]
                public string turbo_cmd = "turbo";

                [JsonProperty("Chat commands to open the skill tree", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<string> chat_cmd = new List<string>() { "st", "skilltree", "skills" };

                [JsonProperty("Chat/console commands to open the score board", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<string> score_chat_cmd = new List<string>() { "score", "scoreboard" };
            }

            [JsonProperty("XP settings")]
            public XPSettings xp_settings = new XPSettings();

            public class XPSettings
            {
                [JsonProperty("XP Loss settings")]
                public XPLossSettings xp_loss_settings = new XPLossSettings();

                public class XPLossSettings
                {
                    [JsonProperty("Percentage of xp into their current level that players lose when killed in PVP")]
                    public double pvp_death_penalty = 20;

                    [JsonProperty("Percentage of xp into their current level that players lose when killed in PVE")]
                    public double pve_death_penalty = 20;

                    [JsonProperty("Percentage of xp into their current level that players lose when they suicide")]
                    public double suicide_death_penalty = 0;

                    [JsonProperty("Permission based modifiers [1.0 = no reduced amount]")]
                    public Dictionary<string, double> xp_loss_override = new Dictionary<string, double>();
                }                

                [JsonProperty("Permissions to adjust xp gain modifiers (skilltree.<perm>) [1.0 is default modifier]")]
                public Dictionary<string, double> xp_perm_modifier = new Dictionary<string, double>();

                [JsonProperty("How long should the xp be displayed for")]
                public float xp_display_time = 1f;

                [JsonProperty("Colour of the xp text when unmodified")]
                public string xp_display_col_unmodified = "FFFFFF";

                [JsonProperty("Colour of the xp text when modified")]
                public string xp_display_col_modified = "00b6ff";

                [JsonProperty("Prevent XP loss when dying at an event hosted by EventManager")]
                public bool prevent_xp_loss = true;

                [JsonProperty("Enable xp drop hud for players by default")]
                public bool enable_xp_drop_by_default = true;

                [JsonProperty("Restrict XP gain to the tools listed in their respective tools list?")]
                public bool white_listed_tools_only = false;

                [JsonProperty("Require grown plants to be ripe to provide xp")]
                public bool ripe_required = true;

                [JsonProperty("Give xp for crafting ingredients? [Requires Cooking.cs]")]
                public bool cooking_award_xp_ingredients = false;

                [JsonProperty("List of meals that will not provide xp [Requires Cooking.cs]")]
                public List<string> cooking_black_list = new List<string>();

                [JsonProperty("Experience sources - Set xp value to 0 to disable for that type", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public XPSources xp_sources = new XPSources();

                [JsonProperty("Night time settings")]
                public NightTimeGains night_settings = new NightTimeGains();

                public class NightTimeGains
                {
                    [JsonProperty("Modifier for xp gained at night [1.0 = standard]")]
                    public float night_xp_gain_modifier = 1f;

                    [JsonProperty("Modifier for woodcutting yield at night [1.0 = standard]")]
                    public float night_woodcutting_yield_modifier = 1f;

                    [JsonProperty("Modifier for mining yield at night [1.0 = standard]")]
                    public float night_mining_yield_modifier = 1f;

                    [JsonProperty("Modifier for skinning yield at night [1.0 = standard]")]
                    public float night_skinning_yield_modifier = 1f;

                    [JsonProperty("Modifier for harvesting yield at night [1.0 = standard]")]
                    public float night_harvesting_yield_modifier = 1f;

                    [JsonProperty("Should the harvesting yield increase include player-grown plants?")]
                    public bool include_grown_harvesting = false;
                }

                [JsonProperty("Allow players with god mode to receive xp?")]
                public bool allow_godemode_xp = true;

                [JsonProperty("Decimal places for the xp to be rounded to")]
                public int xp_rounding = 2;
            }

            [JsonProperty("Wipe and plugin update settings")]
            public WipeUpdate wipe_update_settings = new WipeUpdate();

            public class WipeUpdate
            {
                [JsonProperty("Erase all data on wipe - wipes everything")]
                public bool erase_data_on_wipe = false;

                [JsonProperty("Erase ExtraPockets storage on wipe")]
                public bool erase_ExtraPockets_on_wipe = true;

                [JsonProperty("Refund skill points on server wipe")]
                public bool refund_sp_on_wipe = true;

                [JsonProperty("Give the player with the highest xp bonus skill points next wipe")]
                public bool bonus_skill_points = false;

                [JsonProperty("How many skill points should they receive for winning?")]
                public int bonus_skill_points_amount = 5;

                [JsonProperty("Automatically add new trees from the default config?")]
                public bool auto_update_trees = true;

                [JsonProperty("Automatically add new nodes from the default config?")]
                public bool auto_update_nodes = true;

                [JsonProperty("Starting skill points")]
                public int starting_skill_points = 0;
            }

            [JsonProperty("Rested XP Settings")]
            public RestXPSettings rested_xp_settings = new RestXPSettings();

            public class RestXPSettings
            {
                [JsonProperty("Give players who have been offline a bonus to xp gain when they log in next?")]
                public bool rested_xp_enabled = true;

                [JsonProperty("Rested xp pool to accumulate per hour offline")]
                public double rested_xp_per_hour = 1000;

                [JsonProperty("Bonus xp rate while rested (until the rested xp pool is depleted) [0.25 = 25% bonus]")]
                public double rested_xp_rate = 0.25;

                [JsonProperty("Maximum xp a player can have in their rested pool [0 = no limit]")]
                public double rested_xp_pool_max = 25000;

                [JsonProperty("Reset rested xp pools on wipe?")]
                public bool rested_xp_reset_on_wipe = false;

                [JsonProperty("Modifiers based on permissions to adjust the rested xp value [1.0 = 100% increase. 0.0 = no increase]")]
                public Dictionary<string, float> rested_xp_modifier_perm_mod = new Dictionary<string, float>();
            }

            [JsonProperty("Tools and Black list/White list settings")]
            public ToolAndListSettings tools_black_white_list_settings = new ToolAndListSettings();

            public class ToolAndListSettings
            {
                [JsonProperty("Global black list - these items will not gain xp and benefits at all")]
                public List<string> black_listed_gather_items = new List<string>();

                [JsonProperty("Black listed parts for component and electrical luck abilities", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<string> comp_blacklist = new List<string>();

                [JsonProperty("Power tool modifiers (chainsaw and jackhammer). 1 = full xp/buffs. 0 = off. 0.5 = half xp/buffs.")]
                public PowerTools power_tool_modifier = new PowerTools()
                {
                    skinning_perk_modifier = 0.0f,
                    mining_perk_modifier = 0.0f,
                    woodcutting_perk_modifier = 0.0f
                };

                [JsonProperty("Extra Pockets black list - disallows items that match")]
                public List<string> black_list = new List<string>();

                [JsonProperty("Extra Pockets white list - will only allow items that match")]
                public List<string> white_list = new List<string>();

                [JsonProperty("Extra pockets button anchors")]
                public ExtraPocketsButtonAnchors extra_pockets_button_anchor = new ExtraPocketsButtonAnchors();

                [JsonProperty("A black list of items that will not be refunded when using the thrifty tinkerer buff", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<string> craft_refund_blacklist = new List<string>() { "gunpowder", "explosives", "sulfur" };

                [JsonProperty("A black list of items that will not be duplicated while using the thirfty duplicator buff", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<string> craft_duplicate_blacklist = new List<string>() { "gunpowder", "explosives", "sulfur" };

                [JsonProperty("Woodcutting tools - Tools that meet whitelist requirements and work with the reduced durability ability", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<string> wc_tools = new List<string>();

                [JsonProperty("Mining tools - Tools that meet whitelist requirements and work with the reduced durability ability", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<string> mining_tools = new List<string>();

                [JsonProperty("Skinning tools - Tools that meet whitelist requirements and work with the reduced durability ability", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<string> skinning_tools = new List<string>();
            }

            [JsonProperty("Effect settings")]
            public EffectSettings effect_settings = new EffectSettings();

            public class EffectSettings
            {
                [JsonProperty("Instant repair effect")]
                public string repair_effect = "assets/bundled/prefabs/fx/build/repair_full_metal.prefab";

                [JsonProperty("Level up effect")]
                public string level_effect = "assets/prefabs/misc/halloween/lootbag/effects/gold_open.prefab";

                [JsonProperty("Node unlock effect")]
                public string skill_point_unlock_effect = "assets/prefabs/misc/halloween/lootbag/effects/loot_bag_upgrade.prefab";

                [JsonProperty("Node level effect")]
                public string skill_point_level_effect = "assets/prefabs/misc/easter/painted eggs/effects/eggpickup.prefab";
            }

            [JsonProperty("Better Chat settings")]
            public BetterChatSettings betterchat_settings = new BetterChatSettings();

            public class BetterChatSettings
            {
                [JsonProperty("Format for BetterChat title showing the playeres level. Set to null to disable. {0} is the colour value and {1} is the player level value")]
                public string better_title_format = "<color=#{0}>[Lv.{1}]</color>";

                [JsonProperty("Default colour for BetterChat xp titles")]
                public string better_title_default_col = "0cb072";

                [JsonProperty("Colour for BetterChat xp titles for players who are max level")]
                public string better_title_max_col = "32ff00";
            }

            [JsonProperty("Loot settings")]
            public LootTables loot_settings = new LootTables();

            public class LootTables
            {
                [JsonProperty("Mining luck loot table", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<LootItems> mining_loot_table = new List<LootItems>();

                [JsonProperty("Woodcutting luck loot table", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<LootItems> wc_loot_table = new List<LootItems>();

                [JsonProperty("Skinning luck loot table", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<LootItems> skinning_loot_table = new List<LootItems>();

                [JsonProperty("Fishing luck loot table", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<LootItems> fishing_loot_table = new List<LootItems>();

                [JsonProperty("Whitelist of loot crates to trigger the spawn chance for components, electronics and scrap. Set to null to not use the whitelist.", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<string> loot_crate_whitelist = new List<string>();
            }

            [JsonProperty("Ultimate settings")]
            public UltimateSettings ultimate_settings = new UltimateSettings();

            public class UltimateSettings
            {
                [JsonProperty("Background colour for the ultimate node")]
                public string ultimate_node_background_col = "1 0.8741453 0 1";

                [JsonProperty("Ultimate settings for woodcutting")]
                public WoodcuttingUltimate ultimate_woodcutting = new WoodcuttingUltimate();

                [JsonProperty("Ultimate settings for mining")]
                public MiningUltimate ultimate_mining = new MiningUltimate();

                [JsonProperty("Ultimate settings for vehicle")]
                public VehicleUltimate ultimate_vehicle = new VehicleUltimate();

                [JsonProperty("Ultimate settings for medical")]
                public MedicalUltimate ultimate_medical = new MedicalUltimate();

                [JsonProperty("Ultimate settings for harvesting")]
                public HarvesterUltimate ultimate_harvesting = new HarvesterUltimate();

                [JsonProperty("Ultimate settings for scavenger")]
                public Scav_Ultimate ultimate_scavenger = new Scav_Ultimate();

                [JsonProperty("Ultimate settings for combat")]
                public CombatUltimate ultimate_combat = new CombatUltimate();

                [JsonProperty("Ultimate settings for skinning")]
                public SkinningUltimate ultimate_skinning = new SkinningUltimate();

                [JsonProperty("Ultimate settings for build craft")]
                public BuildCraftUltimate ultimate_buildCraft = new BuildCraftUltimate();
            }

            [JsonProperty("Misc settings")]
            public MiscSettings misc_settings = new MiscSettings();

            public class MiscSettings
            {
                [JsonProperty("BotReSpawn profile and xp list", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public Dictionary<string, double> botrespawn_profiles = new Dictionary<string, double>();

                [JsonProperty("Human NPC's name that can be used to open the skill tree")]
                public string npc_name = "";

                [JsonProperty("Should we add a button to the pump bar to open SkillTree?")]
                public bool button_to_pump_bar = true;
            }

            [JsonProperty("NpcSpawn (by KpucTaji) settings")]
            public BetterNPC betternpc_settings = new BetterNPC();

            public class BetterNPC
            {
                [JsonProperty("Give xp based on the name of a NpcSpawn, rather than the scientist type?")]
                public bool betternpc_give_xp = true;

                [JsonProperty("Dictionary of NPC names and the value that they provide")]
                public Dictionary<string, double> NPC_xp_table = new Dictionary<string, double>(StringComparer.InvariantCultureIgnoreCase);
            }

            [JsonProperty("Skill tree", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, Configuration.TreeInfo> trees = new Dictionary<string, Configuration.TreeInfo>();

            [JsonProperty("Leveling information. Y value must be set to 2 or 3")]
            public ExperienceInfo level = new ExperienceInfo();

            [JsonProperty("Notification settings")]
            public NotificationSettings notification_settings = new NotificationSettings();

            public class ExtraPocketsButtonAnchors
            {
                public string x_min = "185.4";
                public string y_min = "25.4";
                public string x_max = "227.4";
                public string y_max = "67.4";
            }

            public class HorseStats
            {
                public bool Increase_Horse_RunSpeed = true;
                public bool Increase_Horse_MaxSpeed = true;
                public bool Increase_Horse_TrotSpeed = false;
                public bool Increase_Horse_TurnSpeed = false;
                public bool Increase_Horse_WalkSpeed = false;
            }

            public class NotificationSettings
            {
                [JsonProperty("Settings for Notify plugin")]
                public NotifySettings notifySettings = new NotifySettings();
            }

            public class NotifySettings
            {
                [JsonProperty("Language key and message type to display when a player gains a level")]
                public KeyValuePair<string, int> level_up_notification = new KeyValuePair<string, int>("NotifyLevelGained", 0);               

            }

            public class CustomCurrency
            {
                public string displayName = "";
                public string shortname = "";
                public ulong skin = 0;
            }

            public class TreeInfo
            {
                public bool enabled = true;
                public Dictionary<string, NodeInfo> nodes = new Dictionary<string, NodeInfo>();
                public TreeInfo(Dictionary<string, NodeInfo> nodes, bool enabled = true)
                {
                    this.nodes = nodes;
                    this.enabled = enabled;
                }
                public class NodeInfo
                {
                    public bool enabled;
                    public int max_level;
                    public int tier;
                    public float value_per_buff;
                    public KeyValuePair<Buff, BuffType> buff_info;
                    public string icon_url;
                    public ulong skin;
                    public Permissions permissions;
                    public NodeInfo(bool enabled, int max_level, int tier, float value_per_buff, KeyValuePair<Buff, BuffType> buff_info, string icon_url, ulong skin, Permissions permissions = null)
                    {
                        this.enabled = enabled;
                        this.max_level = max_level;
                        this.tier = tier;
                        this.value_per_buff = value_per_buff;
                        this.buff_info = buff_info;
                        this.icon_url = icon_url;
                        this.permissions = permissions;
                        this.skin = skin;
                    }
                }
            }            

            public class PowerTools
            {
                public float mining_perk_modifier;
                public float woodcutting_perk_modifier;
                public float skinning_perk_modifier;
            }

            public class ExperienceInfo
            {
                public double x = 0.07;
                public int y = 2;
                public Dictionary<int, double> xp_table = new Dictionary<int, double>();
                public void CalculateTable(int max_level)
                {
                    for (int i = 0; i <= max_level; i++)
                    {
                        if (xp_table.ContainsKey(i))
                        {
                            var newValue = Math.Floor(Math.Pow(i / x, y));
                            if (xp_table[i] != newValue)
                            {
                                xp_table[i] = newValue;
                                UpdatedTable = true;
                            }
                        }

                        else xp_table.Add(i, Math.Floor(Math.Pow(i / x, y)));
                    }
                }
                public int GetLevel(double xp)
                {
                    return Convert.ToInt32(Math.Floor(x * Math.Sqrt(xp)));
                }

                public double GetLevelStartXP(int level)
                {
                    return Math.Pow(level / x, y);
                }

                public static bool UpdatedTable = false;
            }

            public class XPSources
            {
                public double NodeHit = 12;
                public double NodeHitFinal = 50;
                public double TreeHit = 8;
                public double TreeHitFinal = 40;
                public double SkinHit = 10;
                public double SkinHitFinal = 50;
                public double CollectWildPlant = 30;
                public double CollectGrownPlant = 5;
                public double BuildingBlockDeployed = 0;
                public double FishCaught = 100;
                public double Crafting = 0.25;
                public double ScientistNormal = 150;
                public double TunnelDweller = 125;
                public double ScientistHeavy = 300;
                public double SmallAnimal = 20;
                public double MediumAnimal = 50;
                public double LargeAnimal = 100;
                public double RoadSign = 10;
                public double Barrel = 20;
                public double Scarecrow = 100;
                public double Mission = 1000;
                public double BradleyAPC = 1000;
                public double LootHackedCrate = 200;
                public double LootHeliCrate = 250;
                public double LootBradleyCrate = 50;
                public double CookingMealXP = 10;
                public double RaidableBaseCompletion_Easy = 100;
                public double RaidableBaseCompletion_Medium = 200;
                public double RaidableBaseCompletion_Hard = 300;
                public double RaidableBaseCompletion_Expert = 400;
                public double RaidableBaseCompletion_Nightmare = 500;
                public double Win_HungerGames = 2000;
                public double Win_ScubaArena = 2000;
                public double Win_Skirmish = 2000;
                public double Gut_Fish = 10;
                public double default_botrespawn = 100;
                public double crate_basic = 0;
                public double crate_elite = 50;
                public double crate_mine = 0;
                public double crate_normal = 0;
                public double crate_normal_2 = 0;
                public double crate_normal_2_food = 0;
                public double crate_normal_2_medical = 0;
                public double crate_tools = 0;
                public double crate_underwater_advanced = 100;
                public double crate_underwater_basic = 25;
                public double crate_ammunition = 0;
                public double crate_food_1 = 0;
                public double crate_food_2 = 0;
                public double crate_fuel = 0;
                public double crate_medical = 0;
                public double supply_drop = 500;
                public double Harbor_Event_Winner = 2000f;
                public double Junkyard_Event_Winner = 2000f;
                public double PowerPlant_Event_Winner = 2000f;
                public double Satellite_Event_Winner = 2000f;
                public double Water_Event_Winner = 2000f;
                public double Air_Event_Winner = 2000f;
                public double Armored_Train_Winner = 2000f;
                public double Convoy_Winner = 2000f;
                public double SurvivalArena_Winner = 2000f;
                public double swipe_card_level_1 = 50;
                public double swipe_card_level_2 = 100;
                public double swipe_card_level_3 = 250;
                public double boss_monster = 1000;
                public double Zombie = 100;
            }

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        public class Permissions
        {
            public string description;
            public Dictionary<int, PermissionInfo> perms = new Dictionary<int, PermissionInfo>();

            public Permissions(string description, Dictionary<int, PermissionInfo> perms)
            {
                this.description = description;
                this.perms = perms;
            }
        }

        public class PermissionInfo
        {
            public Dictionary<string, string> perms_list = new Dictionary<string, string>();

            // Key = perm. Value = displayName.
            public PermissionInfo(Dictionary<string, string> perms_to_add)
            {
                this.perms_list = perms_to_add;
            }
        }

        public class LootItems
        {
            public string shortname;
            public int min;
            public int max;
            public int dropWeight;
            public string displayName;
            public ulong skin;
            public LootItems(string shortname, int min, int max, ulong skin = 0, string displayName = null, int dropWeight = 100)
            {
                this.shortname = shortname;
                this.min = min;
                this.max = max;
                this.skin = skin;
                this.displayName = displayName;
                this.dropWeight = dropWeight;
            }
        }

        public class PumpBar
        {
            [JsonProperty("Default xp bar offset - this is for any new player connecting to the server")]
            public xp_bar_offset offset_default = new xp_bar_offset()
            {
                min_x = -365.713f,
                min_y = 20f,
                max_x = -233.287f,
                max_y = 42f
            };

            [JsonProperty("Anchor points")]
            public XP_Bar_Anchors anchor_default = new XP_Bar_Anchors()
            {
                anchor_min = "1 0",
                anchor_max = "1 0"
            };

            [JsonProperty("Colour of the pump bar")]
            public string pump_bar_colour = "0.5471698 0.3533202 0 0.6078432";

            [JsonProperty("Font set for the pump bar")]
            public string pump_bar_font = "robotocondensed-regular.ttf";

            [JsonProperty("Font size for the pump bar")]
            public int pump_bar_font_size = 10;
        }

        public class BuildCraftUltimate
        {
            [JsonProperty("Chance that a card with lower access will successfully unlock the door (the card is damaged regardless) [%]")]
            public int success_chance = 100;

            [JsonProperty("Notify the player when their ultimate failed")]
            public bool notify_fail = true;
        }

        public class SkinningUltimate
        {
            [JsonProperty("How long should each buff last for [0 = off]?")]
            public Dictionary<AnimalBuff, float> enabled_buffs = new Dictionary<AnimalBuff, float>();

            [JsonProperty("Wolf perk: what health scale bonus should the player receive per team member near by [1.0 is 100%]?")]
            public float wolf_health_scale = 0.25f;

            [JsonProperty("Wolf perk: How close do teammates need to be in order to contribute to the perk [radius]?")]
            public float wolf_team_dist = 30f;

            [JsonProperty("Bear perk: Maximum health of the overshield")]
            public float bear_overshield_max = 50f;

            [JsonProperty("Stag perk: Maximum distance that the perk can detect dangerous entities from [radius]")]
            public float stag_danger_dist = 30f;

            [JsonProperty("Stag perk: Time between procs [seconds]")]
            public float stag_timer = 10f;

            [JsonProperty("Stag perk: Draw the enemy location?")]
            public bool stag_draw_enemy = true;

            [JsonProperty("Boar perk: Blacklist of components for the boar buff")]
            public List<string> boar_blackList = new List<string>();

            [JsonProperty("Boar perk: Chance when collecting mushrooms and berries that a player [%]")]
            public float boar_chance = 2f;

            [JsonProperty("Boar perk: Minimum quantity to give")]
            public int boar_min_quantity = 1;

            [JsonProperty("Boar perk: Maximum quantity to give")]
            public int boar_max_quantity = 4;
        }

        public class CombatUltimate
        {
            [JsonProperty("What scale of damage should the player receive as health [1.0 = 100%]")]
            public float health_scale = 0.01f;

            [JsonProperty("Should the healing effect of the ultimate work against players")]
            public bool players_enabled = true;

            [JsonProperty("Should the healing effect of the ultimate work against animals")]
            public bool animals_enabled = true;

            [JsonProperty("Should the healing effect of the ultimate work against scientists")]
            public bool scientists_enabled = true;
        }

        public class Scav_Ultimate
        {
            [JsonProperty("List of items that you dont want the perk to recycle")]
            public List<string> item_blacklist = new List<string>();

            [JsonProperty("Scrap items that have a unique name?")]
            public bool scrap_named_items = false;

            [JsonProperty("Scrap items that have a non-default skin?")]
            public bool scrap_skinned_items = false;
        }

        public class HarvesterUltimate
        {
            [JsonProperty("Chat command that players can use to set their plant genes")]
            public string gene_chat_command = "setgenes";

            [JsonProperty("Cooldown between ultimate triggers. Set to 0 if you want all plants to have their genes adjusted [seconds]")]
            public float cooldown = 0f;

            [JsonProperty("Notify a player in chat when they go on cooldown (recommended for longer cooldowns)")]
            public bool notify_on_cooldown = false;
        }

        public class MedicalUltimate
        {
            [JsonProperty("Chance for the player to resurrect [out of 100]")]
            public float resurrection_chance = 50f;

            [JsonProperty("Delay between resurrections after a successful resurrection [seconds]")]
            public float resurrection_delay = 1200f;
        }

        public class VehicleUltimate
        {
            [JsonProperty("Reduction percentage [1.0 == full reduction]")]
            public float reduce_by = 1f;
        }

        public class WoodcuttingUltimate
        {
            [JsonProperty("Award xp for each tree that the perk cuts down?")]
            public bool award_xp = false;

            [JsonProperty("Distance from the player (radius) that trees will be cut down?")]
            public float distance_from_player = 10f;
        }

        public class MiningUltimate
        {
            [JsonProperty("Distance from the player (radius) that nodes will appear (radius)")]
            public float distance_from_player = 200f;

            [JsonProperty("Cooldown time on the ability (seconds)")]
            public float cooldown = 60f;

            [JsonProperty("How many seconds should the marked ores appear on the players hud?")]
            public float hud_time = 60f;

            [JsonProperty("Text size")]
            public int text_size = 12;

            [JsonProperty("Chat command to find the nodes.")]
            public string find_node_cmd = "locatenodes";

            [JsonProperty("Automatically trigger the mining ultimate when the player equips a pickaxe (still abides by the cooldown time)")]
            public bool trigger_on_item_change = false;

            [JsonProperty("List of tools to trigger the ultimate")]
            public List<string> tools_list = new List<string>();
        }        

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            config.level.CalculateTable(config.general_settings.max_player_level > 0 ? config.general_settings.max_player_level : 100);
            config.trees = DefaultTrees;

            config.general_settings.level_rewards.Add(100, new LevelReward(new Dictionary<string, string>() { ["say <color=#ffae00>{name}</color> reached level <color=#4cff03>100</color>!"] = "You have reached a milestone level!" }, new List<string>() { "say Test data reset."}));

            config.tools_black_white_list_settings.wc_tools = new List<string>()
            {
                "hatchet", "axe.salvaged", "stonehatchet", "chainsaw"
            };

            config.ultimate_settings.ultimate_mining.tools_list = DefaultUltimateToolsList;

            config.tools_black_white_list_settings.mining_tools = new List<string>()
            {
                "pickaxe", "stone.pickaxe", "icepick.salvaged", "jackhammer"
            };

            config.tools_black_white_list_settings.skinning_tools = new List<string>()
            {
                "knife.bone", "knife.butcher", "knife.combat", "hatchet"
            };

            config.loot_settings.mining_loot_table = DefaultLootItems;

            config.loot_settings.wc_loot_table = DefaultLootItems;

            config.loot_settings.skinning_loot_table = DefaultLootItems;

            config.loot_settings.fishing_loot_table = DefaultLootItems;

            config.tools_black_white_list_settings.comp_blacklist = new List<string>() { "generic", "chassis", "glue", "bleach", "ducttape", "sticks", "vehicle.chassis", "vehicle.module", "vehicle.chassis.4mod", "vehicle.chassis.3mod", "vehicle.chassis.2mod", "electric.generator.small" };

            config.buff_settings.primitive_weapons = new List<string>() { "spear.stone", "spear.wooden", "bone.club", "bow.hunting" };

            config.buff_settings.animals = new List<string>() { "boar", "horse", "stag", "chicken", "wolf", "bear", "scarecrow", "polarbear" };

            config.loot_settings.loot_crate_whitelist = new List<string>()
            {
                "assets/bundled/prefabs/radtown/crate_elite.prefab",
                "assets/bundled/prefabs/radtown/crate_normal.prefab",
                "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
                "assets/bundled/prefabs/radtown/crate_tools.prefab",
                "assets/bundled/prefabs/radtown/crate_underwater_advanced.prefab",
                "assets/bundled/prefabs/radtown/underwater_labs/crate_elite.prefab",
                "assets/bundled/prefabs/radtown/underwater_labs/crate_normal.prefab",
                "assets/bundled/prefabs/radtown/underwater_labs/crate_normal_2.prefab",
                "assets/bundled/prefabs/radtown/underwater_labs/crate_tools.prefab"
            };

            config.tools_black_white_list_settings.black_listed_gather_items = new List<string>() { "bone.club" };

            config.general_settings.respec_cost_override.Add("vip", Math.Round(config.general_settings.respec_cost / 2, 0));
            config.general_settings.max_skill_points_override.Add("vip", config.general_settings.max_skill_points + (Convert.ToInt32(config.general_settings.max_skill_points * 0.2)));
            config.general_settings.max_skill_points_override.Add("nolimit", 0);
            config.xp_settings.xp_loss_settings.xp_loss_override.Add("vip", 0.5);
            config.xp_settings.xp_perm_modifier.Add("vip", 1.0);

            config.buff_settings.no_refund_item_skins = new List<ulong>() { 2529344523, 2546992444, 2546992685 };
            config.ultimate_settings.ultimate_scavenger.item_blacklist = DefaultScavengerUltimateBlacklist;
            config.ultimate_settings.ultimate_skinning.enabled_buffs = DefaultAnimalBuffs;

            config.xp_settings.cooking_black_list = new List<string>() { "ingredient bag" };
        }

        List<string> DefaultUltimateToolsList
        {
            get
            {
                return new List<string>()
                {
                    "pickaxe", "stone.pickaxe", "icepick.salvaged", "jackhammer"
                };
            }
        }

        List<LootItems> DefaultLootItems
        {
            get
            {
                return new List<LootItems>()
                {
                    new LootItems("keycard_blue", 1, 1),
                    new LootItems("keycard_green", 1, 1),
                    new LootItems("keycard_red", 1, 1),
                    new LootItems("lowgradefuel", 1, 10)
                };
            }
        }

        public class LevelReward
        {
            [JsonProperty("List of commands and chat messages that the player receives when reaching the specified level [Left = command. Right = Private message to player]. {id} = steam ID. {name} == name.")]
            public Dictionary<string, string> reward_commands = new Dictionary<string, string>();

            [JsonProperty("List of commands that are fired off when the player data is reset")]
            public List<string> reset_commands = new List<string>();

            public LevelReward(Dictionary<string, string> reward_commands, List<string> reset_commands = null)
            {
                this.reward_commands = reward_commands;
                this.reset_commands = reset_commands;
            }
        }        

        Dictionary<string, float> DefaultUnderwaterChance
        {
            get
            {
                return new Dictionary<string, float>()
                {
                    ["assets/bundled/prefabs/radtown/underwater_labs/crate_elite.prefab"] = 5f,
                    ["assets/bundled/prefabs/radtown/underwater_labs/crate_normal.prefab"] = 5f,
                    ["assets/bundled/prefabs/radtown/underwater_labs/crate_normal_2.prefab"] = 5f,
                    ["assets/bundled/prefabs/radtown/crate_underwater_advanced.prefab"] = 5f,
                    ["assets/bundled/prefabs/radtown/crate_underwater_basic.prefab"] = 5f
                };
            }
        }

        List<LootItems> GetSharkLoot()
        {
            List<LootItems> loot = new List<LootItems>();
            foreach (var item in ItemManager.GetItemDefinitions())
                loot.Add(new LootItems(item.shortname, 1, item.isWearable ? 1 : item.isHoldable ? 1 : UnityEngine.Random.Range(2, 5)));
            return loot;
        }

        public Dictionary<string, List<LootItems>> DeepSeaLooterLootTable;
        public List<LootItems> SharkLootTable;

        Dictionary<string, List<LootItems>> GetUnderwaterLoot()
        {
            Dictionary<string, List<LootItems>> result = new Dictionary<string, List<LootItems>>();

            List<LootItems> items = new List<LootItems>();

            foreach (var item in ItemManager.GetItemDefinitions().Where(x => x.category == ItemCategory.Component))
                items.Add(new LootItems(item.shortname, 1, 3));

            result.Add("assets/bundled/prefabs/radtown/crate_underwater_basic.prefab", items);
            result.Add("assets/bundled/prefabs/radtown/underwater_labs/crate_normal_2.prefab", items);
            result.Add("assets/bundled/prefabs/radtown/underwater_labs/crate_normal.prefab", items);

            foreach (var item in ItemManager.GetItemDefinitions().Where(x => x.category == ItemCategory.Electrical || x.category == ItemCategory.Weapon || x.category == ItemCategory.Attire))
            {
                if (item.category == ItemCategory.Attire || item.category == ItemCategory.Weapon) items.Add(new LootItems(item.shortname, 1, 1));
                else items.Add(new LootItems(item.shortname, 1, 3));
            }
            result.Add("assets/bundled/prefabs/radtown/crate_underwater_advanced.prefab", items);
            result.Add("assets/bundled/prefabs/radtown/underwater_labs/crate_elite.prefab", items);

            return result;
        }

        List<string> OldLinks = new List<string>() 
        {            
            "https://www.dropbox.com/s/vc5ing7pbtxv6er/Paladinskill_07_nobg.png?dl=1",
            "https://imgur.com/zAkmSub.png",
            "https://www.dropbox.com/s/jkhionm6r7gfj95/Paladinskill_12_nobg.png?dl=1",
            "https://www.dropbox.com/s/k0v94pupgo0qmwy/Paladinskill_20_nobg.png?dl=1",
            "https://www.dropbox.com/s/6qp1kfvtw4ixsk7/Assassinskill_16_nobg.png?dl=1",
            "https://www.dropbox.com/s/muim3nqo63nkwqk/Paladinskill_02_nobg.png?dl=1",
            "https://www.dropbox.com/s/qsivpq886epcypo/Mageskill_18_nobg.png?dl=1",
            "https://www.dropbox.com/s/fin32eer1d88f0f/Engineerskill_32_nobg.png?dl=1",
            "https://imgur.com/hO7fA3z.png",
            "https://www.dropbox.com/s/hjq9kird3uxohlo/Archerskill_17_nobg.png?dl=1",
            "https://www.dropbox.com/s/o3zb8kfu53r4m3k/Warriorskill_50_nobg.png?dl=1",
            "https://www.dropbox.com/s/gkp6k8zwfh3rc41/Archerskill_29_nobg.png?dl=1",
            "https://www.dropbox.com/s/qybq86hkdoab14r/Assassinskill_26_nobg.png?dl=1",
            "https://www.dropbox.com/s/dnebyrhp7bugl61/Mageskill_35_nobg.png?dl=1",
            "https://www.dropbox.com/s/095ij4c20yomy8x/Mageskill_49_nobg.png?dl=1",
            "https://www.dropbox.com/s/st1blgav08kgnrb/Regrowth.png?dl=1",
            "https://www.dropbox.com/s/71bryupwygpuv9z/Druideskill_11_nobg.png?dl=1",
            "https://imgur.com/2AnzivI.png",
            "https://www.dropbox.com/s/v85ya0x8roo1q67/33_Critical_strike_nobg.png?dl=1",
            "https://imgur.com/sA5KtYp.png",
            "https://www.dropbox.com/s/k7e89kpwroskp1f/34_Critical_strike2_nobg.png?dl=1",
            "https://www.dropbox.com/s/s9lr3wl5ej5u9k7/36_Critical_strike4_nobg.png?dl=1",
            "https://www.dropbox.com/s/igcf7tqn1ezolnh/35_Critical_strike3_nobg.png?dl=1",
            "https://www.dropbox.com/s/t0ahbcc13b24yr0/Assassinskill_21_nobg.png?dl=1",
            "https://www.dropbox.com/s/1hzsha39lqkulrp/Archerskill_10_nobg.png?dl=1",
            "https://www.dropbox.com/s/lio5lk1sqn32k1a/Warriorskill_13_nobg.png?dl=1",
            "https://imgur.com/GNdYVEI.png",
            "https://imgur.com/C4mseOn.png",
            "https://imgur.com/jsO5eGQ.png",
            "https://www.dropbox.com/s/prwf810f4cqun9l/Engineerskill_33_nobg.png?dl=1",
            "https://www.dropbox.com/s/hvd4su438s54erh/Druideskill_02_nobg.png?dl=1",
            "https://imgur.com/YNkQ2mb.png",
            "https://www.dropbox.com/s/v0a5rw82gjmxi1t/Warlock_15_nobg.png?dl=1",
            "https://www.dropbox.com/s/oh6tnjojkgjehvd/Druideskill_03_nobg.png?dl=1",
            "https://www.dropbox.com/s/apydr8pj51z030x/Archerskill_09_nobg.png?dl=1",
            "https://imgur.com/fH2sIUp.png",
            "https://imgur.com/Ys5zO7o.png",
            "https://www.dropbox.com/s/h9ixd1ug2en4pn4/Paladinskill_17_nobg.png?dl=1",
            "https://imgur.com/7Ko8KLg.png",
            "https://www.dropbox.com/s/dqaxiz73p8uwf7q/Mageskill_03_nobg.png?dl=1",
            "https://www.dropbox.com/s/8e1wtvbnroayreq/Archerskill_20_nobg.png?dl=1",
            "https://imgur.com/N5ixdsD.png",
            "https://www.dropbox.com/s/xgqae0jhp1exp8w/Mageskill_38_nobg.png?dl=1",
            "https://www.dropbox.com/s/g25krvk8c5n99d9/Warriorskill_29_nobg.png?dl=1",
            "https://www.dropbox.com/s/18sptrssfukaf7s/Priestskill_48_nobg.png?dl=1",
            "https://imgur.com/yCpYOFB.png",
            "https://www.dropbox.com/s/y948cf4lfhx2aoy/Druideskill_23_nobg.png?dl=1",
            "https://imgur.com/UjF7tZp.png",
            "https://www.dropbox.com/s/596bfn1pzm1hzhn/Engineerskill_24_nobg.png?dl=1",
            "https://imgur.com/TKrf3oQ.png",
            "https://imgur.com/Kmbv9gS.png",
            "https://imgur.com/wG7cG8X.png",
            "https://imgur.com/xMp1fhd.png",
            "https://imgur.com/cVhnv41.png",
            "https://imgur.com/Utjvw8E.png",
            "https://imgur.com/LvwIvql.png",
            "https://www.dropbox.com/s/amyh43h1p1x62fr/Engineerskill_01_nobg.png?dl=1",
            "https://www.dropbox.com/s/p88fvhnvu3e7x6q/Assassinskill_35_nobg.png?dl=1",
            "https://www.dropbox.com/s/y83duc088snp1d9/Engineerskill_04_nobg.png?dl=1",
            "https://www.dropbox.com/s/dfdymr0lpf201b2/Engineerskill_17_nobg.png?dl=1",
            "https://www.dropbox.com/s/nqhoai1x52ujht3/Engineerskill_15_nobg.png?dl=1",
            "https://www.dropbox.com/s/xvsbehljd76maos/Engineerskill_09_nobg.png?dl=1",
            "https://imgur.com/q9KYN2K.png",
            "https://www.dropbox.com/s/1ca4a25o8yb28za/Archerskill_33_nobg.png?dl=1",
            "https://imgur.com/OFND5GM.png",
            "https://imgur.com/GPfIawP.png",
            "https://imgur.com/RXuJ7LI.png",
            "https://www.dropbox.com/s/8not58eqky3s7nv/Engineerskill_23_nobg.png?dl=1",
            "https://www.dropbox.com/s/1htiof75vq9wp2u/Assassinskill_44_nobg.png?dl=1",
            "https://imgur.com/c6L8ASa.png",
            "https://www.dropbox.com/s/swzyy1va1ga06ds/Assassinskill_36_nobg.png?dl=1",
            "https://www.dropbox.com/s/oq8641e4oz6sd41/Druideskill_20_nobg.png?dl=1",
            "https://www.dropbox.com/s/ahn5ji2zzffzowa/Engineerskill_21_nobg.png?dl=1",
            "https://www.dropbox.com/s/uvqzjks7ak673gy/Engineerskill_31_nobg.png?dl=1",
            "https://www.dropbox.com/s/n82viwka4nf2yop/Mageskill_16_nobg.png?dl=1",
            "https://imgur.com/RTARVWQ.png",
            "https://imgur.com/cuzgYOX.png",
            "https://www.dropbox.com/s/a83zlb8l27wvvq8/Warriorskill_49_nobg.png?dl=1",
            "https://imgur.com/AfLYdhS.png",
            "https://imgur.com/KNmzwFE.png",
            "https://imgur.com/UfnqXLe.png",
            "https://imgur.com/V8O6zv6.png",
            "https://imgur.com/Lo690dm.png",
            "https://imgur.com/KONhg85.png",
            "https://imgur.com/akFL8MA.png",
            "https://imgur.com/QbfJgP3.png",
            "https://www.dropbox.com/s/6ov6c33ntgbhqhf/Archerskill_15_nobg.png?dl=1",
            "https://imgur.com/EaABpHv.png",
            "https://imgur.com/bebpjg0.png",
            "https://imgur.com/N7qOlOC.png",
            "https://imgur.com/O0ls6gI.png",
            "https://imgur.com/YyHmSrN.png",
            "https://imgur.com/rmRaZQG.png",
            "https://imgur.com/R4Ik5me.png",
            "https://imgur.com/nT08292.png",
            "https://imgur.com/52AYAmf.png",
            "https://imgur.com/sUrtJJ6.png"
        };


        Dictionary<string, Configuration.TreeInfo> DefaultTrees
        {
            get
            {
                return new Dictionary<string, Configuration.TreeInfo>()
                {
                    ["Mining"] = new Configuration.TreeInfo(new Dictionary<string, Configuration.TreeInfo.NodeInfo>()
                    {
                        ["Amature Miner"] = new Configuration.TreeInfo.NodeInfo(true, 5, 1, 0.05f, new KeyValuePair<Buff, BuffType>(Buff.Mining_Yield, BuffType.Percentage), "https://www.dropbox.com/s/dajctapqtv8bt4z/Amature_Miner.png?dl=1", 2873965665),
                        ["Stroke of luck"] = new Configuration.TreeInfo.NodeInfo(true, 5, 1, 0.05f, new KeyValuePair<Buff, BuffType>(Buff.Node_Spawn_Chance, BuffType.Percentage), "https://www.dropbox.com/s/ym4bktn12jr6t4p/Stroke_of_Luck.png?dl=1", 2873042230),
                        ["Adept Miner"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.075f, new KeyValuePair<Buff, BuffType>(Buff.Mining_Yield, BuffType.Percentage), "https://www.dropbox.com/s/pon14r7e6bvn2kd/Adept_Miner.png?dl=1", 2873042840),
                        ["Instant Mining"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.05f, new KeyValuePair<Buff, BuffType>(Buff.Instant_Mine, BuffType.Percentage), "https://www.dropbox.com/s/x3awxx2vkfun2io/Instant_Mining.png?dl=1", 2873042968),
                        ["Mining Luck"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.01f, new KeyValuePair<Buff, BuffType>(Buff.Mining_Luck, BuffType.Percentage), "https://www.dropbox.com/s/uk7oix7ojnsqyjh/Mining_Luck.png?dl=1", 2873043145),
                        ["Expert Miner"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Mining_Yield, BuffType.Percentage), "https://www.dropbox.com/s/rgoj7fey7xvzrgk/Expert_Miner.png?dl=1", 2873043244),
                        ["Refiner"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Smelt_On_Mine, BuffType.Percentage), "https://www.dropbox.com/s/wbsgo1egl10dwcx/Refiner.png?dl=1", 2873043347),
                        ["Robust pickaxe"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Mining_Tool_Durability, BuffType.Percentage), "https://www.dropbox.com/s/j4nnel0lzonhu9b/Robust_pickaxe.png?dl=1", 2873043495),
                        ["Stone Sense"] = new Configuration.TreeInfo.NodeInfo(true, 1, 4, 1f, new KeyValuePair<Buff, BuffType>(Buff.Mining_Ultimate, BuffType.IO), "https://www.dropbox.com/s/kkge1vpuptc37z4/Stone_Sense.png?dl=1", 2873043666)
                    }),
                    ["Woodcutting"] = new Configuration.TreeInfo(new Dictionary<string, Configuration.TreeInfo.NodeInfo>()
                    {

                        ["Amature Woodcutter"] = new Configuration.TreeInfo.NodeInfo(true, 5, 1, 0.05f, new KeyValuePair<Buff, BuffType>(Buff.Woodcutting_Yield, BuffType.Percentage), "https://www.dropbox.com/s/2s89vo3bmqyefux/Amature_Woodcutter.png?dl=1", 2873043851),
                        ["Adept Woodcutter"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.075f, new KeyValuePair<Buff, BuffType>(Buff.Woodcutting_Yield, BuffType.Percentage), "https://www.dropbox.com/s/nf9reuenek59a6y/Adept_Woodcutter.png?dl=1", 2873043965),
                        ["Instant Woodcutting"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.05f, new KeyValuePair<Buff, BuffType>(Buff.Instant_Chop, BuffType.Percentage), "https://www.dropbox.com/s/7vut5y0vub9e05c/Instant_Woodcutting.png?dl=1", 2873044070),
                        ["Woodcutting Luck"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.01f, new KeyValuePair<Buff, BuffType>(Buff.Woodcutting_Luck, BuffType.Percentage), "https://www.dropbox.com/s/6acfo7hlj0sxviq/Woodcutters_Luck.png?dl=1", 2873044171),
                        ["Expert Woodcutter"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Woodcutting_Yield, BuffType.Percentage), "https://www.dropbox.com/s/tzlbixc5ufbxqjs/Expert_Woodcutter.png?dl=1", 2873044270),
                        ["Chimney"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Woodcutting_Coal, BuffType.Percentage), "https://www.dropbox.com/s/d8ovy2trv5tuipw/Chimney.png?dl=1", 2873044493),
                        ["Tree Regrowth"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.025f, new KeyValuePair<Buff, BuffType>(Buff.Regrowth, BuffType.Percentage), "https://www.dropbox.com/s/kbyk4nhzu7akmhb/Tree_Regrowth.png?dl=1", 2874292571),
                        ["Robust Axe"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Woodcutting_Tool_Durability, BuffType.Percentage), "https://www.dropbox.com/s/4p5gqo7fbfaw9jz/Robust_Axe.png?dl=1", 2873044601),
                        ["Deforestation"] = new Configuration.TreeInfo.NodeInfo(true, 1, 4, 1f, new KeyValuePair<Buff, BuffType>(Buff.Woodcutting_Ultimate, BuffType.IO), "https://www.dropbox.com/s/tww0gg1pwh90qbb/Deforestation.png?dl=1", 2873044743)
                    }),
                    ["Skinning"] = new Configuration.TreeInfo(new Dictionary<string, Configuration.TreeInfo.NodeInfo>()
                    {
                        ["Amature Skinner"] = new Configuration.TreeInfo.NodeInfo(true, 5, 1, 0.05f, new KeyValuePair<Buff, BuffType>(Buff.Skinning_Yield, BuffType.Percentage), "https://www.dropbox.com/s/dxrecqcjlsaqskm/Amature_Skinner.png?dl=1", 2873044870),
                        ["Skilled Tracker"] = new Configuration.TreeInfo.NodeInfo(true, 1, 1, 1f, new KeyValuePair<Buff, BuffType>(Buff.AnimalTracker, BuffType.IO), "https://www.dropbox.com/s/mai6z52eiqqqrwx/Skilled_Tracker.png?dl=1", 2873044977),
                        ["Adept Skinner"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.075f, new KeyValuePair<Buff, BuffType>(Buff.Skinning_Yield, BuffType.Percentage), "https://www.dropbox.com/s/cy3eo6mr6r4gbeb/Adept_Skinner.png?dl=1", 2873045152),
                        ["Instant Skinner"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.05f, new KeyValuePair<Buff, BuffType>(Buff.Instant_Skin, BuffType.Percentage), "https://www.dropbox.com/s/wgic15j3uecdegl/Instant_Skinner.png?dl=1", 2873045291),
                        ["Robust Knife"] = new Configuration.TreeInfo.NodeInfo(true, 5, 1, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Skinning_Tool_Durability, BuffType.Percentage), "https://www.dropbox.com/s/j8fkg0xu1cfz7wd/Robust_Knife.png?dl=1", 2873045413),
                        ["Expert Skinner"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Skinning_Yield, BuffType.Percentage), "https://www.dropbox.com/s/fs66l8mad4uci9p/Expert_Skinner.png?dl=1", 2873045569),
                        ["Survival Chef"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Skin_Cook, BuffType.Percentage), "https://www.dropbox.com/s/eahszj2z8lppx66/Survival_Chef.png?dl=1", 2873046097),
                        ["Steel Knife"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Skinning_Tool_Durability, BuffType.Percentage), "https://www.dropbox.com/s/0c8kf86a43rmo5r/Steel_Knife.png?dl=1", 2873046697),
                        ["Skilled Hunter"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Animal_NPC_Damage, BuffType.Percentage), "https://www.dropbox.com/s/asrhyvazu2ilkqx/Skilled_Hunter.png?dl=1", 2873046221),
                        ["Primal Identity"] = new Configuration.TreeInfo.NodeInfo(true, 1, 4, 1f, new KeyValuePair<Buff, BuffType>(Buff.Skinning_Ultimate, BuffType.IO), "https://www.dropbox.com/s/ba8gmtvr62rek6q/Primal_Identity.png?dl=1", 2873046423),
                        ["Skinning Luck"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.03f, new KeyValuePair<Buff, BuffType>(Buff.Skinning_Luck, BuffType.Percentage), "https://www.dropbox.com/s/mpqadgqy1h6uj38/Shamanskill_09_nobg.v1.png?dl=1", 2912885514),
                    }),
                    ["Harvesting"] = new Configuration.TreeInfo(new Dictionary<string, Configuration.TreeInfo.NodeInfo>()
                    {
                        ["Amature Harvester"] = new Configuration.TreeInfo.NodeInfo(true, 5, 1, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Harvest_Wild_Yield, BuffType.Percentage), "https://www.dropbox.com/s/kpvo6cniebzgvdx/Amature_Harvester.png?dl=1", 2873046943),
                        ["Hobbiest Harvester"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.15f, new KeyValuePair<Buff, BuffType>(Buff.Harvest_Wild_Yield, BuffType.Percentage), "https://www.dropbox.com/s/s5slqgk7sxdd1w3/Hobbiest_Harvester.png?dl=1", 2873047153),
                        ["Amature Farmer"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.075f, new KeyValuePair<Buff, BuffType>(Buff.Harvest_Grown_Yield, BuffType.Percentage), "https://www.dropbox.com/s/sqpubic88t7oyaw/Amature_Farmer.png?dl=1", 2873047337),
                        ["Extra pockets"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 2f, new KeyValuePair<Buff, BuffType>(Buff.ExtraPockets, BuffType.Slots), "https://www.dropbox.com/s/damaguptw7r54r5/Extra_pockets.png?dl=1", 2873047443),
                        ["Expert Harvester"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.2f, new KeyValuePair<Buff, BuffType>(Buff.Harvest_Wild_Yield, BuffType.Percentage), "https://www.dropbox.com/s/t8xms1jaxgoissj/Expert_Harvester.png?dl=1", 2873047547),
                        ["Expert Farmer"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Harvest_Grown_Yield, BuffType.Percentage), "https://www.dropbox.com/s/s1z0p2z80r6bkpn/Expert_Farmer.png?dl=1", 2873047644),
                        ["Fisherman"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Extra_Fish, BuffType.Percentage), "https://www.dropbox.com/s/ohvnyq50bmt46ok/Fisherman.png?dl=1", 2873047745),
                        ["Botanist"] = new Configuration.TreeInfo.NodeInfo(true, 1, 4, 1f, new KeyValuePair<Buff, BuffType>(Buff.Harvester_Ultimate, BuffType.IO), "https://www.dropbox.com/s/8fylvtokuhcj24u/Botanist.png?dl=1", 2873047865),
                        ["Fishing Luck"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.03f, new KeyValuePair<Buff, BuffType>(Buff.Fishing_Luck, BuffType.Percentage), "https://www.dropbox.com/s/lrwsqmsyx7a8653/Shamanskill_14_nobg.v1.png?dl=1", 2912896627),
                    }),
                    ["Medical"] = new Configuration.TreeInfo(new Dictionary<string, Configuration.TreeInfo.NodeInfo>()
                    {
                        ["Bandage Expert"] = new Configuration.TreeInfo.NodeInfo(true, 1, 1, 1f, new KeyValuePair<Buff, BuffType>(Buff.Double_Bandage_Heal, BuffType.IO), "https://www.dropbox.com/s/p1wlvzz5vtyvd24/Bandage_Expert.png?dl=1", 2873048071),
                        ["Radiation Expert"] = new Configuration.TreeInfo.NodeInfo(true, 5, 1, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Radiation_Reduction, BuffType.Percentage), "https://www.dropbox.com/s/g18h9sv1q5wi1t7/Radiation_Expert.png?dl=1", 2873965334),
                        ["Revitalization"] = new Configuration.TreeInfo.NodeInfo(true, 5, 1, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.HealthRegen, BuffType.PerSecond), "https://www.dropbox.com/s/9neapkjx2ntpclm/Revitalization.png?dl=1", 2873048224),
                        ["Flame Retardant"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Fire_Damage_Reduction, BuffType.Percentage), "https://www.dropbox.com/s/mmvn40hp81niuc5/Flame_Retardant.png?dl=1", 2873048354),
                        ["Accident Evasion"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Fall_Damage_Reduction, BuffType.Percentage), "https://www.dropbox.com/s/1n69ohui470smxj/Accident_Evasion.png?dl=1", 2873048450),
                        ["Battle Medic"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Reviver, BuffType.Percentage), "https://www.dropbox.com/s/opmy93244kmtj7j/Battle_Medic.png?dl=1", 2873048560),
                        ["Rugged Up"] = new Configuration.TreeInfo.NodeInfo(true, 1, 3, 1f, new KeyValuePair<Buff, BuffType>(Buff.No_Cold_Damage, BuffType.IO), "https://www.dropbox.com/s/l9hmad91yyb8v3x/Rugged_Up.png?dl=1", 2873048673),
                        ["Perfect Balance"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Fall_Damage_Reduction, BuffType.Percentage), "https://www.dropbox.com/s/vn5rf6prsaviblj/Perfect_Balance.png?dl=1", 2873048757),
                        ["Second Wind"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.05f, new KeyValuePair<Buff, BuffType>(Buff.Wounded_Resist, BuffType.Percentage), "https://www.dropbox.com/s/zh9wcs2alr4uzlm/Second_Wind.png?dl=1", 2873048855),
                        ["Messiah"] = new Configuration.TreeInfo.NodeInfo(true, 1, 4, 1f, new KeyValuePair<Buff, BuffType>(Buff.Medical_Ultimate, BuffType.IO), "https://www.dropbox.com/s/qz76ogz9tayfgwb/Messiah.png?dl=1", 2873048963)

                    }),
                    ["Combat"] = new Configuration.TreeInfo(new Dictionary<string, Configuration.TreeInfo.NodeInfo>()
                    {
                        ["Animal Tamer"] = new Configuration.TreeInfo.NodeInfo(true, 5, 1, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Animal_Damage_Resist, BuffType.Percentage), "https://www.dropbox.com/s/5wgfza2h6d1nxpd/Animal_Tamer.png?dl=1", 2873049142),
                        ["Defence Research"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.05f, new KeyValuePair<Buff, BuffType>(Buff.Human_NPC_Defence, BuffType.Percentage), "https://www.dropbox.com/s/rs66cu2qrawjfjw/Defence_Research.png?dl=1", 2873049362),
                        ["Resourceful"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.02f, new KeyValuePair<Buff, BuffType>(Buff.Free_Bullet_Chance, BuffType.Percentage), "https://www.dropbox.com/s/1vmyged7iu3j9fi/Resourceful.png?dl=1", 2873049496),
                        ["Scientific Breakthrough"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.05f, new KeyValuePair<Buff, BuffType>(Buff.Human_NPC_Damage, BuffType.Percentage), "https://www.dropbox.com/s/910bdsqaon9ja22/Scientific_Breakthrough.png?dl=1", 2873049588),
                        ["Duelist"] = new Configuration.TreeInfo.NodeInfo(true, 5, 1, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Melee_Resist, BuffType.Percentage), "https://www.dropbox.com/s/619ww8i1fu7a28e/Duelist.png?dl=1", 2873049666),
                        ["Lucky Shot"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.01f, new KeyValuePair<Buff, BuffType>(Buff.PVP_Critical, BuffType.Percentage), "https://www.dropbox.com/s/iezzpbs15qbmt1u/Lucky_Shot.png?dl=1", 2873049731),
                        ["Assassin"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.01f, new KeyValuePair<Buff, BuffType>(Buff.PVP_Damage, BuffType.Percentage), "https://www.dropbox.com/s/i39yhkrdvwti1dn/Assassin.png?dl=1", 2873049790),
                        ["Guarded"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.02f, new KeyValuePair<Buff, BuffType>(Buff.PVP_Shield, BuffType.Percentage), "https://www.dropbox.com/s/yufh2ieo4kysb5g/Guarded.png?dl=1", 2873049899),
                        ["Vampiric Tendencies"] = new Configuration.TreeInfo.NodeInfo(true, 1, 4, 1f, new KeyValuePair<Buff, BuffType>(Buff.Combat_Ultimate, BuffType.IO), "https://www.dropbox.com/s/3brv8bohuk75npj/Vampiric_Tendencies.png?dl=1", 2873050024),
                        ["Maintenance"] = new Configuration.TreeInfo.NodeInfo(true, 5, 1, 0.2f, new KeyValuePair<Buff, BuffType>(Buff.Durability, BuffType.Percentage), "https://www.dropbox.com/s/b1bgfqdxe2wunr0/Maintenance.png?dl=1", 2873050116)
                    }),
                    ["Build_Craft"] = new Configuration.TreeInfo(new Dictionary<string, Configuration.TreeInfo.NodeInfo>()
                    {
                        ["Amature Tinkerer"] = new Configuration.TreeInfo.NodeInfo(true, 5, 1, 0.05f, new KeyValuePair<Buff, BuffType>(Buff.Craft_Speed, BuffType.Percentage), "https://www.dropbox.com/s/4hw98tpjmsohf9n/Amature_Tinkerer.png?dl=1", 2873050271),
                        ["Thrifty Renovator"] = new Configuration.TreeInfo.NodeInfo(true, 5, 1, 0.05f, new KeyValuePair<Buff, BuffType>(Buff.Upgrade_Refund, BuffType.Percentage), "https://www.dropbox.com/s/36274c1xkjjtb0f/Thirfty_Renovator.png?dl=1", 2873050381),
                        ["Adept Tinkerer"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.05f, new KeyValuePair<Buff, BuffType>(Buff.Craft_Speed, BuffType.Percentage), "https://www.dropbox.com/s/mikhmlqfx5y7fvl/Adept_Tinkerer.png?dl=1", 2873050490),
                        ["Thrifty Tinkerer"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.05f, new KeyValuePair<Buff, BuffType>(Buff.Craft_Refund, BuffType.Percentage), "https://www.dropbox.com/s/yp73p3ruy4l4oh2/Thrifty_Tinkerer.png?dl=1", 2873050600),
                        ["Researcher"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.05f, new KeyValuePair<Buff, BuffType>(Buff.Research_Refund, BuffType.Percentage), "https://www.dropbox.com/s/armldhxjctbg67w/Research.png?dl=1", 2873050672),
                        ["Expert Tinkerer"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Craft_Speed, BuffType.Percentage), "https://www.dropbox.com/s/8ibntd9n1033dh4/Expert_Tinkerer.png?dl=1", 2873050815),
                        ["Blast Furnace"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Smelt_Speed, BuffType.Percentage), "https://www.dropbox.com/s/p0i3ef8mq4mug05/Blast_Furnace.png?dl=1", 2873050908),
                        ["Primitive Expert"] = new Configuration.TreeInfo.NodeInfo(true, 1, 1, 1f, new KeyValuePair<Buff, BuffType>(Buff.Primitive_Expert, BuffType.IO), "https://www.dropbox.com/s/gvy0dreisfvd9t5/Primitive_Expert.png?dl=1", 2873051010),
                        ["Thrifty Duplicator"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.03f, new KeyValuePair<Buff, BuffType>(Buff.Craft_Duplicate, BuffType.Percentage), "https://www.dropbox.com/s/lzd1p3l8q5t7rx2/Thrifty_Duplicator.png?dl=1", 2873051123),
                        ["Access Granted"] = new Configuration.TreeInfo.NodeInfo(true, 1, 4, 1f, new KeyValuePair<Buff, BuffType>(Buff.Build_Craft_Ultimate, BuffType.IO), "https://www.dropbox.com/s/58hmrgytnntn314/Access_Granted.png?dl=1", 2873051221),
                        ["Blacksmith"] = new Configuration.TreeInfo.NodeInfo(true, 1, 3, 1f, new KeyValuePair<Buff, BuffType>(Buff.MaxRepair, BuffType.IO), "https://www.dropbox.com/s/b4k6p03u77x9d7t/Blacksmith.png?dl=1", 2873051294)

                    }),
                    ["Scavenging"] = new Configuration.TreeInfo(new Dictionary<string, Configuration.TreeInfo.NodeInfo>()
                    {
                        ["Looter"] = new Configuration.TreeInfo.NodeInfo(true, 5, 1, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Extra_Scrap_Barrel, BuffType.Percentage), "https://www.dropbox.com/s/y1cmvfjafenh1du/Looter.png?dl=1", 2873051741),
                        ["Barrel Smasher"] = new Configuration.TreeInfo.NodeInfo(true, 1, 1, 1f, new KeyValuePair<Buff, BuffType>(Buff.Barrel_Smasher, BuffType.IO), "https://www.dropbox.com/s/sebu0m6s5v2sual/Barrel_Smasher.png?dl=1", 2873051884),
                        ["Loot Magnet"] = new Configuration.TreeInfo.NodeInfo(true, 5, 1, 0.2f, new KeyValuePair<Buff, BuffType>(Buff.Loot_Pickup, BuffType.Percentage), "https://www.dropbox.com/s/hqthffkbbm73krh/Loot_Magnet.png?dl=1", 2873965904),
                        ["Lucky Looter"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.05f, new KeyValuePair<Buff, BuffType>(Buff.Extra_Scrap_Crate, BuffType.Percentage), "https://www.dropbox.com/s/wpo449q9tinohgh/Lucky_Looter.png?dl=1", 2873052061),
                        ["Electronics Luck"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.02f, new KeyValuePair<Buff, BuffType>(Buff.Electronic_Chest, BuffType.Percentage), "https://www.dropbox.com/s/zshx9nme86dqrhm/Electronics_Luck.png?dl=1", 2873052180),
                        ["Component Luck"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.02f, new KeyValuePair<Buff, BuffType>(Buff.Component_Chest, BuffType.Percentage), "https://www.dropbox.com/s/vlz5kic6d0qw5df/Components_Luck.png?dl=1", 2873052277),
                        ["Component Salvager"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.02f, new KeyValuePair<Buff, BuffType>(Buff.Component_Barrel, BuffType.Percentage), "https://www.dropbox.com/s/b4xu8xxe7zjwrwj/Component_Salvager.png?dl=1", 2873052390),
                        ["Electronics Salvager"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.02f, new KeyValuePair<Buff, BuffType>(Buff.Electronic_Barrel, BuffType.Percentage), "https://www.dropbox.com/s/jcgwgqzvoxw8y9e/Electronics_Salvager.png?dl=1", 2873052471),
                        ["Optimized Recycling"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.5f, new KeyValuePair<Buff, BuffType>(Buff.Recycler_Speed, BuffType.Seconds), "https://www.dropbox.com/s/3mq7xdj8zmdltp7/Optimized_Recycling.png?dl=1", 2873052554),
                        ["Shredder"] = new Configuration.TreeInfo.NodeInfo(true, 1, 4, 1f, new KeyValuePair<Buff, BuffType>(Buff.Scavengers_Ultimate, BuffType.IO), "https://www.dropbox.com/s/xib7ax3gwo6gg97/Shredder.png?dl=1", 2873052624)
                    }),
                    ["Vehicles"] = new Configuration.TreeInfo(new Dictionary<string, Configuration.TreeInfo.NodeInfo>()
                    {
                        ["Amature Rider"] = new Configuration.TreeInfo.NodeInfo(true, 5, 1, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Riding_Speed, BuffType.Percentage), "https://www.dropbox.com/s/9lj4colvhioznl1/Amature_Rider.png?dl=1", 2873052721),
                        ["Adept Rider"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Riding_Speed, BuffType.Percentage), "https://www.dropbox.com/s/javi1k9ys88iwxg/Adept_Rider.png?dl=1", 2873053592),
                        ["Expert Rider"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.2f, new KeyValuePair<Buff, BuffType>(Buff.Riding_Speed, BuffType.Percentage), "https://www.dropbox.com/s/oxfeh3b1xvqqo7t/Expert_Rider.png?dl=1", 2873053752),
                        ["Economical Pilot"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.03f, new KeyValuePair<Buff, BuffType>(Buff.Heli_Fuel_Rate, BuffType.Percentage), "https://www.dropbox.com/s/0h1hq0nco5dimtv/Economical_Pilot.png?dl=1", 2873054261),
                        ["Hybrid Pilot"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.07f, new KeyValuePair<Buff, BuffType>(Buff.Heli_Fuel_Rate, BuffType.Percentage), "https://www.dropbox.com/s/wgjms949cdlzegm/Hybrid_Pilot.png?dl=1", 2873054373),
                        ["Yachtman"] = new Configuration.TreeInfo.NodeInfo(true, 5, 1, 0.2f, new KeyValuePair<Buff, BuffType>(Buff.Boat_Speed, BuffType.Percentage), "https://www.dropbox.com/s/86096soadnsy2qh/Yachtman.png?dl=1", 2873054539),
                        ["Economical Captain"] = new Configuration.TreeInfo.NodeInfo(true, 5, 1, 0.15f, new KeyValuePair<Buff, BuffType>(Buff.Boat_Fuel_Rate, BuffType.Percentage), "https://www.dropbox.com/s/5f4agk38f2y1ch4/Economical_Captain.png?dl=1", 2873054654),
                        ["Mechanic"] = new Configuration.TreeInfo.NodeInfo(true, 1, 3, 1f, new KeyValuePair<Buff, BuffType>(Buff.Vehicle_Mechanic, BuffType.IO), "https://www.dropbox.com/s/pdpum3gj4sfiowy/Mechanic.png?dl=1", 2873054766),
                        ["Tank"] = new Configuration.TreeInfo.NodeInfo(true, 1, 4, 1f, new KeyValuePair<Buff, BuffType>(Buff.Vehicle_Ultimate, BuffType.IO), "https://www.dropbox.com/s/xdcl74d0e82e32l/Tank.png?dl=1", 2873054991)
                    }),
                    ["Cooking"] = new Configuration.TreeInfo(new Dictionary<string, Configuration.TreeInfo.NodeInfo>()
                    {
                        ["Easily satisfied"] = new Configuration.TreeInfo.NodeInfo(true, 5, 1, 0.2f, new KeyValuePair<Buff, BuffType>(Buff.Extra_Food_Water, BuffType.Percentage), "https://www.dropbox.com/s/473rrrfjnt9huij/Easily_Satisfied.png?dl=1", 2873055105),
                        ["Iron Stomach"] = new Configuration.TreeInfo.NodeInfo(true, 1, 2, 1f, new KeyValuePair<Buff, BuffType>(Buff.Iron_Stomach, BuffType.IO), "https://www.dropbox.com/s/221dsnnubpa2825/Iron_Stomach.png?dl=1", 2873055201),
                        ["Glutton"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.2f, new KeyValuePair<Buff, BuffType>(Buff.Metabolism_Boost, BuffType.Percentage), "https://www.dropbox.com/s/t0r4pttz6b7663c/Glutton.png?dl=1", 2873055301),
                        ["Fruggal Rationer"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.Rationer, BuffType.Percentage), "https://www.dropbox.com/s/azdltk7o99y7mpk/Fruggal_Rationer.png?dl=1", 2873055400)
                    }),
                    ["Underwater"] = new Configuration.TreeInfo(new Dictionary<string, Configuration.TreeInfo.NodeInfo>()
                    {
                        ["Cage Diver"] = new Configuration.TreeInfo.NodeInfo(true, 5, 1, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.SharkResistance, BuffType.Percentage), "https://www.dropbox.com/s/hti5v35qh45lj94/Cage_Diver.png?dl=1", 2873055798),
                        ["Gilled"] = new Configuration.TreeInfo.NodeInfo(true, 5, 1, 60f, new KeyValuePair<Buff, BuffType>(Buff.WaterBreathing, BuffType.Seconds), "https://www.dropbox.com/s/e48z07x10qkmh4s/Gilled.png?dl=1", 2873055882),
                        ["Reckless Diver"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.1f, new KeyValuePair<Buff, BuffType>(Buff.SharkResistance, BuffType.Percentage), "https://www.dropbox.com/s/k204yk4b6upj0r4/Reckless_Diver.png?dl=1", 2873056389),
                        ["Shark Veterinarian"] = new Configuration.TreeInfo.NodeInfo(true, 5, 2, 0.02f, new KeyValuePair<Buff, BuffType>(Buff.SharkSkinner, BuffType.Percentage), "https://www.dropbox.com/s/zjiqshz9tzpgh82/Shark_Veterinarian.png?dl=1", 2873056528),
                        ["Treasure Hunter"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.02f, new KeyValuePair<Buff, BuffType>(Buff.DeepSeaLooter, BuffType.Percentage), "https://www.dropbox.com/s/po3zsz763hopkjc/Treasure_Hunter.png?dl=1", 2873056606),
                        ["Nimble Fingers"] = new Configuration.TreeInfo.NodeInfo(true, 1, 3, 1f, new KeyValuePair<Buff, BuffType>(Buff.InstantUntie, BuffType.IO), "https://www.dropbox.com/s/3m853nqxq2qmoih/Nimble_Fingers.png?dl=1", 2873056697),
                        ["Aquatic Combatant"] = new Configuration.TreeInfo.NodeInfo(true, 5, 3, 0.10f, new KeyValuePair<Buff, BuffType>(Buff.UnderwaterDamageBonus, BuffType.Percentage), "https://www.dropbox.com/s/683npn5fwvkbtni/Aquatic_Combatant.png?dl=1", 2873056786)
                    })
                };
            }
        }

        Dictionary<AnimalBuff, float> DefaultAnimalBuffs
        {
            get
            {
                return new Dictionary<AnimalBuff, float>()
                {
                    [AnimalBuff.Bear] = 120f,
                    [AnimalBuff.Chicken] = 600f,
                    [AnimalBuff.Boar] = 1200f,
                    [AnimalBuff.Stag] = 300f,
                    [AnimalBuff.Wolf] = 600f,
                    [AnimalBuff.PolarBear] = 600f
                };
            }
        }

        List<string> DefaultScavengerUltimateBlacklist
        {
            get
            {
                return new List<string>()
                {
                    "lowgradefuel",
                    "targeting.computer",
                    "cctv.camera"
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintToConsole("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            PrintToConsole($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion

        #region Data

        PluginInfo pcdData;
        private DynamicConfigFile PCDDATA;

        void Init()
        {
            PCDDATA = Interface.Oxide.DataFileSystem.GetFile(this.Name);
            permission.RegisterPermission("skilltree.chat", this);
            permission.RegisterPermission("skilltree.xp", this);
            permission.RegisterPermission("skilltree.tree", this);
            permission.RegisterPermission(perm_admin, this);
            permission.RegisterPermission("skilltree.bag.keepondeath", this);
            permission.RegisterPermission("skilltree.notitles", this);
            permission.RegisterPermission(perm_no_scoreboard, this);

            foreach (var perm in config.general_settings.max_skill_points_override.Keys)
            {
                if (!permission.PermissionExists("skilltree." + perm, this)) permission.RegisterPermission("skilltree." + perm, this);
            }
            foreach (var perm in config.general_settings.respec_cost_override.Keys)
            {
                if (!permission.PermissionExists("skilltree." + perm, this)) permission.RegisterPermission("skilltree." + perm, this);
            }
            foreach (var perm in config.xp_settings.xp_perm_modifier)
            {
                if (!permission.PermissionExists("skilltree." + perm.Key, this)) permission.RegisterPermission("skilltree." + perm.Key, this);
            }
            foreach (var perm in config.rested_xp_settings.rested_xp_modifier_perm_mod)
            {
                if (!permission.PermissionExists("skilltree." + perm.Key)) permission.RegisterPermission("skilltree." + perm.Key, this);
            }
            permission.RegisterPermission("skilltree.all", this);
            foreach (var tree in config.trees)
            {
                permission.RegisterPermission("skilltree." + tree.Key.ToString(), this);
            }
            if (!config.general_settings.require_tree_perms)
            {
                Unsubscribe(nameof(OnGroupPermissionRevoked));
                Unsubscribe(nameof(OnGroupPermissionGranted));
                Unsubscribe(nameof(OnUserPermissionRevoked));
                Unsubscribe(nameof(OnUserPermissionGranted));
            }

            if (config.xp_settings.xp_sources.Harbor_Event_Winner == 0) Unsubscribe("OnHarborEventWinner");
            if (config.xp_settings.xp_sources.Junkyard_Event_Winner == 0) Unsubscribe("OnJunkyardEventWinner");
            if (config.xp_settings.xp_sources.Satellite_Event_Winner == 0) Unsubscribe("OnSatDishEventWinner");
            if (config.xp_settings.xp_sources.Water_Event_Winner == 0) Unsubscribe("OnWaterEventWinner");
            if (config.xp_settings.xp_sources.Air_Event_Winner == 0) Unsubscribe("OnAirEventWinner");
            if (config.xp_settings.xp_sources.PowerPlant_Event_Winner == 0) Unsubscribe("OnPowerPlantEventWinner");
            if (config.xp_settings.xp_sources.Armored_Train_Winner == 0) Unsubscribe("OnArmoredTrainEventWin");
            if (config.xp_settings.xp_sources.Convoy_Winner == 0) Unsubscribe("OnConvoyEventWin");
            if (config.xp_settings.xp_sources.SurvivalArena_Winner == 0) Unsubscribe(nameof(OnSurvivalArenaWin));
            if (config.xp_settings.xp_sources.boss_monster == 0) Unsubscribe(nameof(OnBossKilled));

        }

        void Unload()
        {
            SaveNewNodesToConfig();
            DeepSeaLooterLootTable.Clear();
            SharkLootTable.Clear();
            if (BasePlayer.activePlayerList.Count > 0)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    try
                    {
                        CuiHelper.DestroyUi(player, "SkillTree");
                        CuiHelper.DestroyUi(player, "XP_Tick");
                        CuiHelper.DestroyUi(player, "respec_confirmation");
                        CuiHelper.DestroyUi(player, "SkillTreeXPBar");
                        CuiHelper.DestroyUi(player, "ui_mover");
                        CuiHelper.DestroyUi(player, "SkillTreeBackPanel");
                        CuiHelper.DestroyUi(player, "SkillTree_PlayerMenu");
                        CuiHelper.DestroyUi(player, "ExtraPocketsButton");
                        CuiHelper.DestroyUi(player, "ScoreBoardPanel");
                        CuiHelper.DestroyUi(player, "ScoreboardBackPanel");
                        CuiHelper.DestroyUi(player, "SkillTree_UltimateMenu");
                        CuiHelper.DestroyUi(player, "SkillTree_MedicalUltimate_ResurrectionButton");
                        CuiHelper.DestroyUi(player, "SkillTree_MedicalUltimate_Failed");
                        CuiHelper.DestroyUi(player, "Plant_Gene_Select");
                        CuiHelper.DestroyUi(player, "Plant_Gene_Select_background");
                        CuiHelper.DestroyUi(player, "Overshield_main");
                        CuiHelper.DestroyUi(player, "StagDangerUI");
                        CuiHelper.DestroyUi(player, "UnderwaterBreathCounter");

                        DoClear(player);
                        player.EndLooting();
                        LoggingOff(player);
                    }
                    catch { Puts($"Failed to update data for {player.userID}"); }
                }
            }
            var objects = GameObject.FindObjectsOfType(typeof(Regen));
            if (objects != null)
            {
                foreach (var gameObj in objects)
                {
                    GameObject.DestroyImmediate(gameObj);
                }
            }
            foreach (var horse in HorseStats.ToList())
            {
                if (horse.Value.horse != null && horse.Value.horse.IsAlive()) RestoreHorseStats(horse.Value.horse);
            }
            foreach (var heli in tracked_helis.ToList())
            {
                if (heli.Value != null && heli.Value.IsAlive()) heli.Value.fuelPerSec = default_heli_fuel_rate;
            }
            foreach (var boat in tracked_rhibs.ToList())
            {
                if (boat.Value != null && boat.Value.IsAlive()) boat.Value.fuelPerSec = default_rhib_fuel_rate;
            }
            foreach (var boat in tracked_rowboats.ToList())
            {
                if (boat.Value != null && boat.Value.IsAlive()) boat.Value.fuelPerSec = default_rowboat_fuel_rate;
            }
            foreach (var chatcommand in config.chat_commands.score_chat_cmd)
            {
                cmd.RemoveChatCommand(chatcommand, this);
                cmd.RemoveConsoleCommand(chatcommand, this);
            }
            foreach (var chatcommand in config.chat_commands.chat_cmd)
            {
                cmd.RemoveChatCommand(chatcommand, this);
            }
            cmd.RemoveChatCommand(config.chat_commands.turbo_cmd, this);
            SaveData();

            ScoreBoard.scoreList.Clear();
            healers.Clear();
            MiningUltimateCooldowns.Clear();

            foreach (var entity in reduced_damage_entities)
            {
                RestoreSkinToChildren(entity.Value);
            }
            reduced_damage_entities.Clear();

            cmd.RemoveChatCommand(config.ultimate_settings.ultimate_mining.find_node_cmd, this);
            cmd.RemoveChatCommand(config.ultimate_settings.ultimate_harvesting.gene_chat_command, this);
            ItemDefs.Clear();
        }

        void Loaded()
        {
            LoadData();
        }

        void SaveData()
        {
            PCDDATA.WriteObject(pcdData);
        }

        void LoadData()
        {
            try
            {
                pcdData = Interface.Oxide.DataFileSystem.ReadObject<PluginInfo>(this.Name);
            }
            catch
            {
                Puts("Couldn't load player data, creating new Playerfile");
                pcdData = new PluginInfo();
            }
        }

        class PluginInfo
        {
            public ulong highest_player;
            public Dictionary<ulong, PlayerInfo> pEntity = new Dictionary<ulong, PlayerInfo>();
        }

        class PlayerInfo
        {
            public string name;
            public double xp;
            public int current_level;
            public int achieved_level;
            public int available_points;
            public Dictionary<string, int> buff_values = new Dictionary<string, int>();
            public bool xp_drops = true;
            public bool xp_hud = true;
            public bool better_chat_enabled = true;
            public xp_bar_offset xp_hud_pos = new xp_bar_offset();
            public List<ItemInfo> pouch_items = new List<ItemInfo>();
            public DateTime logged_off = DateTime.Now;
            public double xp_bonus_pool;
            public bool extra_pockets_button;
            public bool notifications = true;
            public Dictionary<Buff, UltimatePlayerSettings> ultimate_settings = new Dictionary<Buff, UltimatePlayerSettings>();
            public string plant_genes = "gggggg";
            public float respec_multiplier = 0;
        }
        public class UltimatePlayerSettings
        {
            public bool enabled = true;

        }

        public class xp_bar_offset
        {
            public float min_x;
            public float min_y;
            public float max_x;
            public float max_y;
        }

        public class XP_Bar_Anchors
        {
            public string anchor_min = "1 0";
            public string anchor_max = "1 0";
        }

        public ScoreboardInfo ScoreBoard = new ScoreboardInfo();

        public class ScoreboardInfo
        {
            public float lastChecked;
            public Dictionary<ulong, ScoreInfo> scoreList = new Dictionary<ulong, ScoreInfo>();

            public class ScoreInfo
            {
                public string name;
                public double xp;
                public ScoreInfo(string name, double xp)
                {
                    this.name = name;
                    this.xp = xp;
                }
            }
        }

        Dictionary<ulong, TreeInfo> TreeData = new Dictionary<ulong, TreeInfo>();

        class TreeInfo
        {
            public Dictionary<string, NodesInfo> trees = new Dictionary<string, NodesInfo>();
            public int total_points_spent;
        }

        class NodesInfo
        {
            public int points_spent;
            public Dictionary<string, NodeInfo> nodes = new Dictionary<string, NodeInfo>();
        }

        class NodeInfo
        {
            public string description;
            public int level_current;
            public int level_max;
            public int tier;
            public float value_per_buff;
            public KeyValuePair<Buff, BuffType> buffInfo;
        }

        Dictionary<uint, HorseInfo> HorseStats = new Dictionary<uint, HorseInfo>();

        public class HorseInfo
        {
            public RidableHorse horse;
            public float current_maxSpeed;
            public float current_runSpeed;
            public float current_walkSpeed;
            public float current_trotSpeed;
            public float current_turnSpeed;
            public BasePlayer player;
        }

        Dictionary<ulong, BuffDetails> buffDetails = new Dictionary<ulong, BuffDetails>();
        class BuffDetails
        {
            public Dictionary<Buff, float> buff_values = new Dictionary<Buff, float>();
        }

        Dictionary<string, Buff> BuffTypes = new Dictionary<string, Buff>();

        Dictionary<ulong, BoatInfo> boats = new Dictionary<ulong, BoatInfo>();

        class BoatInfo
        {
            public float defaultSpeed;
            public BaseBoat boat;
        }

        Dictionary<string, ItemBlueprint> item_BPs = new Dictionary<string, ItemBlueprint>();
        Dictionary<string, ItemDefinition> ItemDefs = new Dictionary<string, ItemDefinition>();

        List<ulong> notifiedPlayers = new List<ulong>();

        public float default_heli_fuel_rate = 0.5f;
        public float default_rowboat_fuel_rate = 0.1f;
        public float default_rhib_fuel_rate = 0.25f;

        #endregion

        #region Enums

        enum DeathType
        {
            PVE,
            PVP,
            Suicide
        }

        public enum BuffType
        {
            IO,
            Percentage,
            Seconds,
            PerSecond,
            Slots,
            Permission
        }

        public enum Buff
        {
            None,
            Mining_Yield,
            Instant_Mine,
            Smelt_On_Mine,
            Mining_Luck,
            Mining_Tool_Durability,
            Woodcutting_Yield,
            Instant_Chop,
            Woodcutting_Luck,
            Woodcutting_Coal,
            Woodcutting_Tool_Durability,
            Skinning_Yield,
            Instant_Skin,
            Skinning_Tool_Durability,
            Skin_Cook,
            Harvest_Wild_Yield,
            Harvest_Grown_Yield,
            Extra_Fish,
            Double_Bandage_Heal,
            Radiation_Reduction,
            Extra_Food_Water,
            Fire_Damage_Reduction,
            Fall_Damage_Reduction,
            No_Cold_Damage,
            Wounded_Resist,
            Animal_Damage_Resist,
            Riding_Speed,
            Free_Bullet_Chance,
            Primitive_Expert,
            Upgrade_Refund,
            Craft_Speed,
            Research_Refund,
            Craft_Refund,
            Extra_Scrap_Barrel,
            Barrel_Smasher,
            Extra_Scrap_Crate,
            Component_Chest,
            Electronic_Chest,
            Component_Barrel,
            Electronic_Barrel,
            Melee_Resist,
            Iron_Stomach,
            Boat_Speed,
            Recycler_Speed,
            Smelt_Speed,
            Heli_Fuel_Rate,
            Boat_Fuel_Rate,
            Vehicle_Mechanic,
            Reviver,
            Rationer,
            PVP_Critical,
            PVP_Damage,
            PVP_Shield,
            Metabolism_Boost,
            Loot_Pickup,
            Node_Spawn_Chance,
            HealthRegen,
            AnimalTracker,
            ExtraPockets,
            Human_NPC_Damage,
            Animal_NPC_Damage,
            Human_NPC_Defence,
            Craft_Duplicate,
            WaterBreathing,
            SharkResistance,
            SharkSkinner,
            DeepSeaLooter,
            InstantUntie,
            UnderwaterDamageBonus,
            Permission,
            MaxRepair,
            Durability,
            Regrowth,
            Skinning_Luck,
            Fishing_Luck,
            Woodcutting_Ultimate = 991,
            Mining_Ultimate = 992,
            Combat_Ultimate = 993,
            Vehicle_Ultimate = 994,
            Harvester_Ultimate = 995,
            Medical_Ultimate = 996,
            Skinning_Ultimate = 997,
            Build_Craft_Ultimate = 998,
            Scavengers_Ultimate = 999
        }

        //public string[] Trees = { "Mining", "Woodcutting", "Skinning", "Harvesting", "Combat", "Medical", "Build_Craft", "Scavenging", "Vehicles", "Cooking"};

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            List<Buff> buffs = Pool.GetList<Buff>();
            buffs.AddRange(Enum.GetValues(typeof(Buff)).Cast<Buff>());
            Dictionary<string, string> buffMessages = new Dictionary<string, string>();
            foreach (var buff in buffs)
            {
                var str = buff.ToString();
                if (!buffMessages.ContainsKey("UI" + str)) buffMessages.Add("UI" + str, str.Replace("_", " "));
            }
            Pool.FreeList(ref buffs);

            List<string> titles = Pool.GetList<string>();
            titles.AddRange(config.trees.Keys);
            Dictionary<string, string> TitleMessages = new Dictionary<string, string>();
            foreach (var title in titles)
            {
                if (!TitleMessages.ContainsKey(title)) TitleMessages.Add(title, title);
            }
            Pool.FreeList(ref titles);

            List<string> node_names = Pool.GetList<string>();
            foreach (var entry in config.trees)
            {
                foreach (var node in entry.Value.nodes)
                {
                    if (!node_names.Contains(node.Key)) node_names.Add(node.Key);
                }
            }
            foreach (var node in node_names)
            {
                if (!TitleMessages.ContainsKey(node)) TitleMessages.Add(node, node);
            }
            Pool.FreeList(ref node_names);

            Dictionary<string, string> DefaultMessages = new Dictionary<string, string>()
            {
                ["None"] = "This has no buffs.",
                ["Mining_Yield"] = "This skill increases your mining yield by <color=#4214388>{0}%</color> per level.",
                ["Instant_Mine"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level to instantly mine out a node on hit.",
                ["Smelt_On_Mine"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level to refine the mined ore.",
                ["Mining_Luck"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level to receive a random item when you mine out a node.",
                ["Skinning_Luck"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level to receive a random item when you skin out a corpse.",
                ["Fishing_Luck"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level to receive a random item when you catch a fish.",
                ["Mining_Tool_Durability"] = "This skill decreases the durability loss of your mining equipment by <color=#4214388>{0}%</color> per level.",
                ["Mining_Ultimate"] = "<color=#db03cb>ULTIMATE:</color> This skill allows you to locate nodes within a <color=#4214388>{0}m</color> radius.",
                ["Medical_Ultimate"] = "<color=#db03cb>ULTIMATE:</color> This skill gives you a <color=#4214388>{0}%</color> chance to resurrect at your last place of death.",
                ["Harvester_Ultimate"] = "<color=#db03cb>ULTIMATE:</color> This skill gives you the ability to set the genetic composition for plants you deploy{0}.",
                ["Woodcutting_Yield"] = "This skill increases your woodcutting yield by <color=#4214388>{0}%</color> per level.",
                ["Instant_Chop"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level to instantly chop a tree down on hit.",
                ["Regrowth"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level to respawn a chopped tree.",
                ["Woodcutting_Luck"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level to receive a random item when you cut down a tree.",
                ["Woodcutting_Coal"] = "This skill gives you a <color=#4214388>{0}%</color> chance to receive some charcoal while woodcutting.",
                ["Woodcutting_Tool_Durability"] = "This skill decreases the durability loss of your woodcutting equipment by <color=#4214388>{0}%</color> per level.",
                ["Woodcutting_Ultimate"] = "<color=#db03cb>ULTIMATE:</color> This skill will harvest surrounding trees in a <color=#4214388>{0}m</color> radius when you cut a tree down.",
                ["Skinning_Ultimate"] = "<color=#db03cb>ULTIMATE:</color> Killing an animal while this skill is active will give you a temporary buff. <color=#4214388>{0}</color>",
                ["Combat_Ultimate"] = "<color=#db03cb>ULTIMATE:</color> This skill will heal you for <color=#4214388>{0}%</color> of the damage done to certain enemies. Enemies: {1}",
                ["Scavengers_Ultimate"] = "<color=#db03cb>ULTIMATE:</color> This skill automatically recycles components from barrels when broken.",
                ["Build_Craft_Ultimate"] = "<color=#db03cb>ULTIMATE:</color> This skill will allow you to use any coloured swipe card in any reader to access it{0}.",
                ["Skinning_Yield"] = "This skill increases your skinning yield by <color=#4214388>{0}%</color> per level.",
                ["Instant_Skin"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level to instantly skin out the animal on hit.",
                ["Skinning_Tool_Durability"] = "This skill decreases the durability loss of your skinning equipment by <color=#4214388>{0}%</color> per level",
                ["Skin_Cook"] = "This skill gives you a <color=#4214388>{0}%</color> per level chance of receiving your meat cooked, rather than raw, while skinning.",
                ["Permission"] = "",
                ["Harvest_Wild_Yield"] = "This skill increases your harvesting yield by <color=#4214388>{0}%</color> per level while harvesting wild collectibles.",
                ["Harvest_Grown_Yield"] = "This skill increases your harvesting yield by <color=#4214388>{0}%</color> per level while harvesting grown plants.",
                ["Extra_Fish"] = "This skill gives you a <color=#4214388>{0}%</color> per level chance of receiving an extra fish when you catch a fish.",
                ["Double_Bandage_Heal"] = "This skill will double the amount of healing received from bandages.",
                ["Radiation_Reduction"] = "This skill will reduce radiation damage received by <color=#4214388>{0}%</color> per level.",
                ["Extra_Food_Water"] = "This skill will increase the amount of calories and hydration received by <color=#4214388>{0}%</color> per level when eating food.",

                ["WaterBreathing"] = "This skill will allow you to breath underwater for <color=#4214388>{0} seconds</color> per level.",
                ["SharkResistance"] = "This skill will reduce the damage received from sharks by <color=#4214388>{0}%</color> per level.",
                ["SharkSkinner"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level of finding a useful item when skinning a shark.",
                ["DeepSeaLooter"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level of finding a useful item when looting crates underwater.",
                ["InstantUntie"] = "This skill allows you to instantly untie underwater crates.",
                ["UnderwaterDamageBonus"] = "This skill increases the damage done while you are underwater by <color=#4214388>{0}%</color> per level.",

                ["Fire_Damage_Reduction"] = "This skill will reduce fire damage by <color=#4214388>{0}%</color> per level.",
                ["Fall_Damage_Reduction"] = "This skill will reduce fall damage by <color=#4214388>{0}%</color> per level.",
                ["No_Cold_Damage"] = "This skill prevents you from being damaged by the cold.",
                ["Wounded_Resist"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level of immediately getting up after being wounded.",
                ["Animal_Damage_Resist"] = "This skill reduces the damage taken by animals by <color=#4214388>{0}%</color> per level.",
                ["Riding_Speed"] = "This skill increases the speed of your mounted horse by <color=#4214388>{0}%</color> per level.",
                ["Free_Bullet_Chance"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level of not using a bullet while firing.",
                ["Primitive_Expert"] = "This skill makes primitive weapons lose no durability.",
                ["Upgrade_Refund"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level of receiving your building materials back when upgrading your building blocks.",
                ["Craft_Speed"] = "This skill increases your crafting speed by <color=#4214388>{0}%</color> per level.",
                ["MaxRepair"] = "This skill will reset the durability of items to max when repairing them.",
                ["Durability"] = "This skill reduces durability loss by <color=#4214388>{0}%</color> per level.",
                ["Smelt_Speed"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level to smelt ore when a log is burned, in addition to the normal smelt rate.",
                ["Research_Refund"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level of receiving your scrap back when researching.",
                ["Craft_Refund"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level of receiving your components back when crafting.",
                ["Craft_Duplicate"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level of duplicating an item while crafting.",
                ["Extra_Scrap_Barrel"] = "This skill gives you <color=#4214388>{0}%</color> chance per level to receive extra scrap when smashing a barrel.",
                ["Barrel_Smasher"] = "This skill allows you to smash a barrel in 1 hit with any weapon.",
                ["Loot_Pickup"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level to add the loot straight into your inventory when destroying a barrel",
                ["Node_Spawn_Chance"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level to spawn a new node after a node is destroyed.",
                ["HealthRegen"] = "This skill regenerates your health by <color=#4214388>{0}hp</color> per level per second.",
                ["Extra_Scrap_Crate"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level of finding extra scrap in a crate, the first time you loot it.",
                ["Component_Chest"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level of finding components in a crate, the first time you loot it.",
                ["Electronic_Chest"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level of finding electronics in a crate, the first time you loot it.",
                ["Component_Barrel"] = "This skill gives you <color=#4214388>{0}%</color> chance per level of finding extra components when destroying barrels.",
                ["Electronic_Barrel"] = "This skill gives you a <color=#4214388>{0}%</color> chance per level of finding extra electronics when destroying barrels.",
                ["Melee_Resist"] = "This skill will reduce melee damage by <color=#4214388>{0}%</color> per level.",
                ["Iron_Stomach"] = "This skill will prevent you from being sick when consuming raw and spoiled food.",
                ["Recycler_Speed"] = "This skill will increase your recycling tick speed by {0} seconds per level",
                ["Boat_Speed"] = "This skill will allow you to toggle your speed while in a boat by pressing mouse 3 or typing the turbo command. Increases your speed by <color=#4214388>{0}%</color> per level.",
                ["Heli_Fuel_Rate"] = "This skill will reduce your fuel consumption when flying a helicopter by <color=#4214388>{0}%</color> per level.",
                ["Boat_Fuel_Rate"] = "This skill will reduce your fuel consumption when using a boat by <color=#4214388>{0}%</color> per level.",
                ["Vehicle_Mechanic"] = "This skill will allow you to instantly repair vehicles at no cost.",
                ["Vehicle_Ultimate"] = "<color=#db03cb>ULTIMATE:</color> This skill reduces damage dealt to your mounted vehicle by <color=#4214388>{0}%</color>.",
                ["Reviver"] = "This skill will heal a downed player when revived for <color=#4214388>{0}%</color> of their health per level.",
                ["PVP_Shield"] = "Reduce the damage receive in PVP by <color=#4214388>{0}%</color> per level.",
                ["PVP_Critical"] = "This skill will give you a <color=#4214388>{0}%</color> chance per level of critically damaging a player in PVP when hit.",
                ["PVP_Damage"] = "This skill will increase the amount of damage you do in PVP by <color=#4214388>{0}%</color> per level.",
                ["Metabolism_Boost"] = "Increases your calories and hydration by <color=#4214388>{0}%</color> per level.",
                ["Rationer"] = "This skill will provide you with a <color=#4214388>{0}%</color> chance per level chance to receive your consumed items back.",
                ["AnimalTracker"] = "This skill will allow you to track the closest animal to you using the <color=#4214388>/track</color> command.",
                ["ExtraPockets"] = "This skill will provide you pouch that can be accessed via the <color=#4214388>/pouch</color> command. Storage is increased by <color=#4214388>{0}</color> slots per level.",
                ["Human_NPC_Damage"] = "This skill increases the damage done to human NPCs by <color=#4214388>{0}%</color> per level.",
                ["Human_NPC_Defence"] = "This skill decreases the damage done from scientists by <color=#4214388>{0}%</color> per level.",
                ["Animal_NPC_Damage"] = "This skill increases the damage done to animals by <color=#4214388>{0}%</color> per level.",
                ["ItemFound"] = "You found {0}x {1} hiding in the {2}.",
                ["ExtraFish"] = "You received {0} extra {1}!",
                ["CraftRefund"] = "You received a refund on your last craft.",
                ["DuplicateProc"] = "You received an additional <color=#4214388>[{0}]</color> while crafting.",
                ["IronTummy"] = "You eat the {0} and feel ok.",
                ["WoundSave"] = "Your ability prevent you from being wounded.",
                ["LostXP"] = "You lost {0} for dying.",
                ["FreeUpgrade"] = "You received a free upgrade!",
                ["ScrapRefund"] = "You received a scrap refund on your research.",
                ["LevelEarn"] = "You gained a level and earned {0} skill point.\nNew level: {1}",
                ["MorePlayersFound"] = "More than one player found: {0}",
                ["NoMatch"] = "No player was found that matched: {0}",
                ["MaxSP"] = "You have spent the maximum number of skill points allowed.",
                ["MaxedNode"] = "You have already maxed out this node.",
                ["NoPrevTierIncUltimate"] = "You do not have enough points in the previous tier to level this node.\nTier 2 requires {0} points.\nTier 3 requires {1} points.\nUltimate requires {2}.",
                ["MaxedSkillPoints"] = "You do not have enough skill points left to level this node.",
                ["AssignedMaxedSkillPoints"] = "You have already assigned the maximum skill points allowed.",
                ["UnlockedFirstNode"] = "You unlocked the {0} node [1/{1}]",
                ["UnlockedNode"] = "You gained a level in the {0} node [{1}/{2}]",
                ["NoPermsTree"] = "You do not have permission to access to the Skill Tree.",
                ["RespecNoScrap"] = "You do not have enough scrap to respect your skill tree.",
                ["EconNotLoaded"] = "Economics is not loaded. Contact your administrator.",
                ["EconNoCash"] = "You do not have enough cash to respect your skill tree.",
                ["EconErrorCash"] = "Error taking cash from your account.",
                ["SRNotLoaded"] = "ServerRewards is not loaded. Contact your administrator.",
                ["SRNoPoints"] = "You do not have enough points to respect your skill tree.",
                ["SRPointError"] = "Error taking points from your account.",
                ["PaidRespec"] = "You paid {0} to respec.",
                ["NoPermsChat"] = "You do not permissions to use the skill tree chat command.",
                ["GiveXPUsage"] = "Usage: givexp <player> <amount>",
                ["ResetXPUsage"] = "Usage: /resetdata <player>",
                ["XPLastArg"] = "XP amount required as the last argument.",
                ["GaveXP"] = "You were given {0} xp by {1}",
                ["ReceivedXP"] = "You gave {0} {1} xp.",
                ["NoPermsXP"] = "You do not have permission to gain xp on this server.",
                ["PrintXPNone"] = "You are level {0} and have {1}/{2} xp.",
                ["Mining"] = "Mining",
                ["Woodcutting"] = "Woodcutting",
                ["Skinning"] = "Skinning",
                ["Harvesting"] = "Harvesting",
                ["Combat"] = "Combat",
                ["Medical"] = "Medical",
                ["Build_Craft"] = "Build Craft",
                ["Scavenging"] = "Scavenging",
                ["AccessReminder"] = "You can access the Skill Tree menu by typing: <color=#4214388>/{0}</color>",
                ["TurboToggleOn"] = "Toggled boat turbo on.",
                ["TurboToggleOff"] = "Toggled boat turbo off.",
                ["TurboInUse"] = "This boat is already being boosted.",
                ["RespecCost"] = "Respec Cost: <color=#ffb600>{0}</color>",
                ["RespecButton"] = "<color=#ffb600>Respec</color>",
                ["ResetData"] = "Reset the data for {0}.",
                ["ReceivedSP"] = "You received {0} skill points.\nNew available balance: {1}",
                ["GaveSP"] = "You gave {0} skill points to {1}.",
                ["GiveSPUsage"] = "Usage: givesp <player> <amount>",
                ["Rationed"] = "You managed to ration the <color=#ffb600>{0}</color> you just consumed.",
                ["PointsRefunded"] = "Your skill points have been refunded.",
                ["PointsRefundedAll"] = "Refunded all skill points.",
                ["NoPlayersSetup"] = "There are no players setup.",
                ["NodeSpawned"] = "A new node spawned in place of your old one thanks to your buff!",
                ["RespecNoCustom"] = "You do not have enough {0} for this.",
                ["UIToggleXP"] = "Toggle the xp indicator that is displayed when gaining xp.",
                ["ON"] = "ON",
                ["OFF"] = "OFF",
                ["UIToggleXPBar"] = "Toggle the xp pump bar.",
                ["RepositionBar"] = "Reposition the xp pump bar.",
                ["ToggleBagButton"] = "Toggle the ExtraPockets hud button.",
                ["UIClose"] = "CLOSE",
                ["UIChange"] = "CHANGE",
                ["UIPlayerSettings"] = "Player Settings",
                ["TrackWait"] = "You must wait {0} seconds before your next tracking attempt.",
                ["NoAnimals"] = "No animals were found!",
                ["TrackFresh"] = "You see fresh animal tracks leading {0}.",
                ["TrackOlder"] = "You see slightly older animal tracks leading {0}.",
                ["TrackOldest"] = "You see old animal tracks leading {0}.",
                ["LevelReward"] = "You received {0} {1} for reaching level {2}.",
                ["UISkillTree"] = "Skill Tree",
                ["UIBuffInformation"] = "Buff Information",
                ["UITreePointsSpent"] = "Tree Points Spent:",
                ["UITotalPointsSpent"] = "Total Points Spent:",
                ["UIAvailablePoints"] = "Available Points:",
                ["UIRestedXPPool"] = "Rested XP Pool:",
                ["UICurrentLevel"] = "Current Level:",
                ["UIXP"] = "XP:",
                ["ButtonPlayerSettings"] = "Player Settings",
                ["UICost"] = "<color=#ffb600>COST:</color> {0}",
                ["UIScrap"] = "scrap",
                ["UIPoints"] = "points",
                ["UIDollars"] = "$",
                ["UIAreYouSure"] = "Are you sure you want to respec your skills?",
                ["ButtonYes"] = "<color=#ffb600>YES</color>",
                ["ButtonNo"] = "<color=#ffb600>NO</color>",
                ["ToggleNotifications"] = "Receive notifications from the Skill Tree plugin when a buff triggers.",
                ["notificationsOff"] = "You will no longer receive notifications from buff triggers.",
                ["notificationsOn"] = "You will now receive notifications for buff triggers.",
                ["DisabledRegen"] = "Your regen has been disabled for <color=#ff8000>{0} seconds</color> after taking damage.",
                ["UIMaxLevel"] = "Maximum Level: <color=#ffb600>{0}</color>",
                ["UISelectedNode"] = "<color=#f481fa>{0}</color>",
                ["RestedNotification"] = "You feel rested and have a bonus xp rate of <color=#00b2ff>{0}%</color> for <color=#00b2ff>{1}</color> xp.",
                ["SharkStomachFound"] = "You find {0} <color=#4214388>{1}</color> in the sharks stomach.",
                ["some"] = "some",
                ["a"] = "a",
                ["HarvestUltiCDNotification"] = "Your harvesting ultimate is now on cooldown for {0} seconds.",
                ["BuildCraftFailNotify"] = "Your BuildCraft ultimate failed to unlock the door.",
                ["Build_Craft_Ultimate_Description_Addition"] = ". Success chance per swipe: <color=#ffb600>{0}%</color>",
                ["Harvesting_Ultimate_Description_Addition"] = ". Cooldown between plants: <color=#ffb600>{0} seconds</color>",
                ["UltimateSettingsUIDescription"] = "Toggle your {0} Ultimate buff on or off",
                ["Build_Craft_formatted"] = "Build & Craft",
                ["UltimateToggleOnMining"] = "You can locate nodes within <color=#DFF008>{0}m</color> using the chat command: <color=#DFF008>{1}</color> once every <color=#DFF008>{2}</color> seconds.",
                ["UltimateToggleOnVehicle"] = "Your mounted vehicle will take <color=#DFF008>{0}%</color> less damage from all sources.",
                ["UltimateToggleOnMedical"] = "You will now have a <color=#DFF008>{0}%</color> chance of resurrecting at your place of death when you click the RESURRECT button on the death screen.",
                ["UltimateToggleOnHarvester"] = "Your plants will now be deployed with your desired gene set. Type <color=#DFF008>/{0}</color> to set your desired genes. Cooldown: <color=#DFF008>{1} seconds</color>.",
                ["UltimateToggleOnBuildCraft"] = "You can now use any coloured key card on a swipe card reader to access a door. Power is still required. Success chance: <color=#DFF008>{0}%</color>",
                ["UltimateToggleOnWoodcutting"] = "You will now cut down any tree in a <color=#DFF008>{0}m</color> radius.",
                ["UltimateToggleOnScavengers"] = "You will now receive recycled components whenever you destroy a barrel.",
                ["UltimateSettings"] = "<color=#ffb600>Ultimate Settings</color>",
                ["BlacklistedItemsFound"] = "Dropped black listed items to the floor: \n{0}",
                ["WhitelistedItemsNotFound"] = "Dropped non-white listed items to the floor: \n{0}",
                ["PumpBarLevelText"] = "<color=#fbff00>Lv.{0}:</color>",
                ["PumpBarXPText"] = "<color=#FFFFFF>{0} / {1}</color>",
                ["UINextArrow"] = "> >",
                ["UIBackArrow"] = "< <",
                ["FailReload"] = "SkillTree failed to find your player data. Please reconnect to the server...",
                ["TargetFailReload"] = "SkillTree failed to find the target players data. They will need to reconnect to the server for this command to work...",
                ["stgiveitemUsage"] = "Usage: /stgiveitem <target player id> <shortname> <quantity> <skin ID> <Optional: displayName>",
                ["stgiveitemInvalidID"] = "ID: {0} is invalid.",
                ["stgiveitemNoPlayerFound"] = "No player found that matched ID: {0}",
                ["stgiveitemInvalidShortname"] = "Shortname: {0} is invalid.",
                ["stgiveitemQuantityInvalid"] = "Quantity: {0} is invalid.",
                ["stgiveitemSkinInvalid"] = "Skin ID: {0} is invalid.",
                ["popupxpstring"] = "<color=#{0}>+{1} XP</color>",
                ["RegrowthProc"] = "You finish cutting the tree down and it instantly grows back!",
                ["NotifyLevelGained"] = "You have reached level: {0}. Available points: {1}.",
                ["RespecMultiplierMessage"] = "\n<size=10><color=#FF0000>This will increase the cost of your next respec by {0}%</color></size>"
            };

            Dictionary<string, string> langMessages = new Dictionary<string, string>();
            foreach (var kvp in DefaultMessages)
            {
                if (!langMessages.ContainsKey(kvp.Key)) langMessages.Add(kvp.Key, kvp.Value);
            }
            foreach (var kvp in buffMessages)
            {
                if (!langMessages.ContainsKey(kvp.Key)) langMessages.Add(kvp.Key, kvp.Value);
            }
            foreach (var kvp in TitleMessages)
            {
                if (!langMessages.ContainsKey(kvp.Key)) langMessages.Add(kvp.Key, kvp.Value);
            }

            lang.RegisterMessages(langMessages, this);
            DefaultMessages.Clear();
            buffMessages.Clear();
            TitleMessages.Clear();
        }

        #endregion

        #region Hooks

        void OnUserPermissionGranted(string id, string permName) => UpdatePlayerPerms(id, permName);
        void OnUserPermissionRevoked(string id, string permName) => UpdatePlayerPerms(id, permName);
        void OnGroupPermissionGranted(string name, string permName) => UpdatePlayersInGroup(name, permName);
        void OnGroupPermissionRevoked(string name, string permName) => UpdatePlayersInGroup(name, permName);

        void UpdatePlayerPerms(string id, string permName)
        {
            foreach (var perm in config.trees.Keys)
            {
                if (permName.Equals("skilltree." + perm.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    UpdatePlayerPerms(id);
                    return;
                }
            }
            if (permName.Equals("skilltree.all", StringComparison.OrdinalIgnoreCase)) UpdatePlayerPerms(id);
        }

        void UpdatePlayersInGroup(string name, string permName)
        {
            foreach (var perm in config.trees.Keys)
            {
                if (permName.Equals("skilltree." + perm.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var user in permission.GetUsersInGroup(name))
                    {
                        UpdatePlayerPerms(user.Split(' ')[0]);
                    }
                    return;
                }
            }
            if (permName.Equals("skilltree.all", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var user in permission.GetUsersInGroup(name))
                {
                    UpdatePlayerPerms(user.Split(' ')[0]);
                }
                return;
            }
        }

        void UpdatePlayerPerms(string id)
        {
            var player = BasePlayer.activePlayerList.Where(x => x.UserIDString == id).FirstOrDefault();
            if (player != null)
            {
                CuiHelper.DestroyUi(player, "SkillTree");
                CuiHelper.DestroyUi(player, "respec_confirmation");
                CuiHelper.DestroyUi(player, "SkillTreeBackPanel");
                DoClear(player);
                LoggingOff(player);
                HandleNewConnection(player);
            }
        }

        object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (action.Equals("gut", StringComparison.OrdinalIgnoreCase)) AwardXP(player, config.xp_settings.xp_sources.Gut_Fish);
            return null;
        }

        object OnItemUse(Item item, int amountToUse)
        {
            if (Cooking != null && Cooking.IsLoaded && Convert.ToBoolean(Cooking.Call("IsCookingMeal", item))) return null;
            var player = item.GetOwnerPlayer();
            if (player == null) return null;
            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.ContainsKey(Buff.Rationer))
            {
                if (item.info.category == ItemCategory.Food && RollSuccessful(bd.buff_values[Buff.Rationer]) && !config.buff_settings.no_refund_item_skins.Contains(item.skin))
                {
                    var refunded_item = ItemManager.CreateByName(item.info.shortname, amountToUse, item.skin);
                    if (item.name != null) refunded_item.name = item.name;
                    GiveItem(player, refunded_item);
                    //player.GiveItem(refunded_item);                   
                    if (NotificationsOn(player)) PrintToChat(player, string.Format(lang.GetMessage("Rationed", this, player.UserIDString), item.name ?? item.info.displayName.english));
                }
            }
            return null;
        }

        bool NotificationsOn(BasePlayer player)
        {
            if (!pcdData.pEntity.ContainsKey(player.userID) || pcdData.pEntity[player.userID].notifications) return true;
            return false;
        }

        void OnPlayerRevive(BasePlayer reviver, BasePlayer player)
        {
            if (reviver == null || player == null) return;
            BuffDetails bd;
            if (buffDetails.TryGetValue(reviver.userID, out bd) && bd.buff_values.ContainsKey(Buff.Reviver))
            {
                BasePlayer revived_player = player;
                NextTick(() =>
                {
                    if (revived_player == null) return;
                    var healthFor = 100 * bd.buff_values[Buff.Reviver];
                    if (healthFor > revived_player.health) revived_player.SetHealth(healthFor);
                });
            }

        }

        void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (newItem == null)
            {
                Timer timer;
                if (MiningUltimate_AutoTrigger_Timers.TryGetValue(player.userID, out timer))
                {
                    if (timer != null && !timer.Destroyed) timer.Destroy();
                    MiningUltimate_AutoTrigger_Timers.Remove(player.userID);
                }
                return;
            }

            if (!IsPickaxe(newItem.info.shortname)) return;

            TriggerMiningUltimateFromItem(player);
        }

        bool IsPickaxe(string shortname)
        {
            return config.ultimate_settings.ultimate_mining.tools_list.Contains(shortname);
        }

        Dictionary<ulong, Timer> MiningUltimate_AutoTrigger_Timers = new Dictionary<ulong, Timer>();

        void OnPlayerAssist(BasePlayer target, BasePlayer player) => OnPlayerRevive(player, target);

        void OnMissionSucceeded(BaseMission mission, BaseMission.MissionInstance missionInstance, BasePlayer player)
        {
            AwardXP(player, config.xp_settings.xp_sources.Mission);
        }

        void OnNewSave(string filename)
        {
            if (config.wipe_update_settings.bonus_skill_points && pcdData.pEntity != null && pcdData.pEntity.Count > 0)
            {
                var highest_player = 0ul;
                var highest_xp = 0d;
                foreach (var kvp in pcdData.pEntity)
                {
                    if (kvp.Value.xp > highest_xp)
                    {
                        highest_xp = kvp.Value.xp;
                        highest_player = kvp.Key;
                    }
                    try
                    {
                        RunResetCommands(kvp.Key.ToString(), kvp.Value.achieved_level);
                    }
                    catch
                    {
                        Puts($"Failed to run Reset command for: {kvp.Key}");
                    }
                }
                Puts($"The player with the highest score is {highest_player} with {highest_xp} xp achieved.");
                pcdData.highest_player = highest_player;
            }
            if (config.wipe_update_settings.refund_sp_on_wipe) ResetSkills();

            foreach (var pi in pcdData.pEntity)
            {
                pi.Value.respec_multiplier = 0;
                if (config.wipe_update_settings.erase_ExtraPockets_on_wipe) pi.Value.pouch_items.Clear();
                if (config.rested_xp_settings.rested_xp_reset_on_wipe)
                {
                    pi.Value.xp_bonus_pool = 0;
                    pi.Value.logged_off = DateTime.Now;
                }
            }

            if (config.wipe_update_settings.erase_data_on_wipe)
            {
                ResetAllData();
            }
        }

        object OnHealingItemUse(MedicalTool tool, BasePlayer player)
        {
            if (player == null || player.IsNpc || !player.userID.IsSteamId()) return null;
            var item = tool.GetItem();
            if (item == null || item.info.shortname != "bandage") return null;
            BuffDetails bd;
            if (!buffDetails.TryGetValue(player.userID, out bd))
            {
                //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {player.displayName}[{player.UserIDString}] - OnHealingItemUse. [Online = {player.IsConnected}]", this, true);
                if (player.IsConnected) PrintToChat(player, lang.GetMessage("FailReload", this, player.UserIDString));
                return null;
            }
            if (bd.buff_values.ContainsKey(Buff.Double_Bandage_Heal))
            {
                if (!healers.Contains(player)) healers.Add(player);
            }
            return null;
        }

        List<BasePlayer> healers = new List<BasePlayer>();

        object OnPlayerHealthChange(BasePlayer player, float oldValue, float newValue)
        {
            if (player == null || player.IsNpc || !player.userID.IsSteamId()) return null;
            if (newValue < oldValue) return null;
            if (healers.Contains(player))
            {
                healers.Remove(player);
                player.Heal(newValue - oldValue);
            }
            if (HasAnimalBuff(player, AnimalBuff.Wolf) && player.Team != null && player.Team.teamID > 0)
            {
                List<BasePlayer> nearby_players = Pool.GetList<BasePlayer>();
                nearby_players.AddRange(FindEntitiesOfType<BasePlayer>(player.transform.position, config.ultimate_settings.ultimate_skinning.wolf_team_dist).Where(x => x.Team != null && x.Team.teamID == player.Team.teamID));

                Unsubscribe("OnPlayerHealthChange");
                player.Heal((newValue - oldValue) * (nearby_players.Count * config.ultimate_settings.ultimate_skinning.wolf_health_scale));
                Subscribe("OnPlayerHealthChange");

                Pool.FreeList(ref nearby_players);
            }

            return null;
        }

        object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item) => HandleDispenser(dispenser, player, item);

        object HandleDispenser(ResourceDispenser dispenser, BasePlayer player, Item item, bool bonus = false)
        {
            if (player.IsNpc || !player.userID.IsSteamId() || dispenser == null || item == null) return null;
            BuffDetails bd;
            var tool = player.GetActiveItem();
            if (tool != null && config.tools_black_white_list_settings.black_listed_gather_items.Contains(tool.info.shortname)) return null;
            if (!buffDetails.TryGetValue(player.userID, out bd))
            {
                //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {player.displayName}[{player.UserIDString}] - HandleDispenser. [Online = {player.IsConnected}]", this, true);
                if (player.IsConnected) PrintToChat(player, lang.GetMessage("FailReload", this, player.UserIDString));
                return null;
            }
            if (bd == null || bd.buff_values == null) return null;
            HeldEntity heldEntity = player.GetHeldEntity();
            var gather_modifier = 1f;
            if (dispenser.gatherType == ResourceDispenser.GatherType.Tree)
            {
                if (heldEntity != null && heldEntity is Chainsaw)
                {
                    if (config.tools_black_white_list_settings.power_tool_modifier.woodcutting_perk_modifier == 0) return null;
                    gather_modifier = config.tools_black_white_list_settings.power_tool_modifier.woodcutting_perk_modifier;
                }

                if (!bonus)
                {
                    if (config.xp_settings.white_listed_tools_only && config.tools_black_white_list_settings.wc_tools.Count > 0)
                    {
                        if (tool != null && config.tools_black_white_list_settings.wc_tools.Contains(tool.info.shortname)) AwardXP(player, config.xp_settings.xp_sources.TreeHit * gather_modifier, dispenser.baseEntity);
                    }
                    else AwardXP(player, config.xp_settings.xp_sources.TreeHit * gather_modifier, dispenser.baseEntity);
                }
                if (bd.buff_values.ContainsKey(Buff.Woodcutting_Yield))
                {
                    if (Interface.CallHook("STCanReceiveYield", player, dispenser.baseEntity) == null)
                    {
                        var amount = item.amount * (bd.buff_values[Buff.Woodcutting_Yield] * gather_modifier) + (TOD_Sky.Instance.IsNight ? item.amount * (config.xp_settings.night_settings.night_woodcutting_yield_modifier - 1) : 0);
                        if (amount > 0.5)
                        {
                            if (amount < 1) amount = 1;
                            item.amount += Convert.ToInt32(amount);
                        }
                    }
                }

                if (!bonus && bd.buff_values.ContainsKey(Buff.Instant_Chop) && RollSuccessful((bd.buff_values[Buff.Instant_Chop] * gather_modifier)))
                {
                    foreach (var r in dispenser.containedItems)
                    {
                        if (r.amount < 1) continue;
                        var bonus_amount = Convert.ToInt32(r.amount + (r.amount * (TOD_Sky.Instance.IsNight ? config.xp_settings.night_settings.night_woodcutting_yield_modifier - 1 : 0)) + (r.amount * (bd.buff_values.ContainsKey(Buff.Woodcutting_Yield) ? bd.buff_values[Buff.Woodcutting_Yield] : 0)));
                        if (r.itemDef.shortname == item.info.shortname) item.amount += bonus_amount;
                        else
                        {
                            //player.GiveItem(ItemManager.CreateByName(r.itemDef.shortname, amount_to_give));
                            if (bonus_amount > 0) GiveItem(player, ItemManager.CreateByName(r.itemDef.shortname, bonus_amount));
                        }

                        r.amount = 0;
                    }
                }
                if (bd.buff_values.ContainsKey(Buff.Woodcutting_Coal) && RollSuccessful((bd.buff_values[Buff.Woodcutting_Coal] * gather_modifier)))
                {
                    GiveItem(player, ItemManager.CreateByName("charcoal", item.amount));
                    //player.GiveItem(ItemManager.CreateByName("charcoal", item.amount));
                }
            }
            if (dispenser.gatherType == ResourceDispenser.GatherType.Ore)
            {
                if (heldEntity != null && heldEntity is Jackhammer)
                {
                    if (config.tools_black_white_list_settings.power_tool_modifier.mining_perk_modifier == 0) return null;
                    gather_modifier = config.tools_black_white_list_settings.power_tool_modifier.mining_perk_modifier;
                }

                if (!bonus)
                {
                    if (config.xp_settings.white_listed_tools_only && config.tools_black_white_list_settings.mining_tools.Count > 0)
                    {
                        if (tool != null && config.tools_black_white_list_settings.mining_tools.Contains(tool.info.shortname)) AwardXP(player, config.xp_settings.xp_sources.NodeHit * gather_modifier, dispenser.baseEntity);
                    }
                    else AwardXP(player, config.xp_settings.xp_sources.NodeHit * gather_modifier, dispenser.baseEntity);
                }

                item.amount += TOD_Sky.Instance.IsNight ? Convert.ToInt32(item.amount * (config.xp_settings.night_settings.night_mining_yield_modifier - 1)) : 0;

                if (bd.buff_values.ContainsKey(Buff.Mining_Yield))
                {
                    HandleMiningYield(dispenser, player, item, bd, gather_modifier);
                }

                if (!bonus && bd.buff_values.ContainsKey(Buff.Instant_Mine) && RollSuccessful((bd.buff_values[Buff.Instant_Mine] * gather_modifier)))
                {
                    HandleInstantMining(dispenser, player, item, bd);
                }
                if (dispenser.baseEntity != null && !string.IsNullOrEmpty(dispenser.baseEntity.ShortPrefabName) && dispenser.baseEntity.ShortPrefabName != "stone-ore" && bd.buff_values.ContainsKey(Buff.Smelt_On_Mine) && RollSuccessful((bd.buff_values[Buff.Smelt_On_Mine] * gather_modifier)))
                {
                    HandleSmeltOnMine(player, item);
                }
            }
            if (dispenser.gatherType == ResourceDispenser.GatherType.Flesh)
            {
                if (heldEntity != null && (heldEntity is Jackhammer || heldEntity is Chainsaw))
                {
                    if (config.tools_black_white_list_settings.power_tool_modifier.skinning_perk_modifier == 0) return null;
                    gather_modifier = config.tools_black_white_list_settings.power_tool_modifier.skinning_perk_modifier;
                }
                bool isFinalHit = dispenser.containedItems.Where(x => x.amount > 0).FirstOrDefault() == null;
                if (!bonus)
                {
                    if (config.xp_settings.white_listed_tools_only && config.tools_black_white_list_settings.skinning_tools.Count > 0)
                    {
                        if (tool != null && config.tools_black_white_list_settings.skinning_tools.Contains(tool.info.shortname)) AwardXP(player, (isFinalHit ? config.xp_settings.xp_sources.SkinHitFinal : config.xp_settings.xp_sources.SkinHit) * gather_modifier, dispenser.baseEntity);
                    }
                    else AwardXP(player, (isFinalHit ? config.xp_settings.xp_sources.SkinHitFinal : config.xp_settings.xp_sources.SkinHit) * gather_modifier, dispenser.baseEntity);
                }

                if (isFinalHit && dispenser.baseEntity.ShortPrefabName.Equals("shark.corpse") && bd.buff_values.ContainsKey(Buff.SharkSkinner) && RollSuccessful(bd.buff_values[Buff.SharkSkinner]))
                {
                    var randomItem = GetSharkLoot().GetRandom();
                    var _item = ItemManager.CreateByName(randomItem.shortname, randomItem.max == 1 ? 1 : UnityEngine.Random.Range(randomItem.min > 1 ? randomItem.min : 1, randomItem.max));
                    player.GiveItem(_item);
                    PrintToChat(player, String.Format(lang.GetMessage("SharkStomachFound", this, player.UserIDString), _item.amount > 1 ? lang.GetMessage("some", this, player.UserIDString) : lang.GetMessage("a", this, player.UserIDString), _item.info.displayName.english));
                }

                item.amount += TOD_Sky.Instance.IsNight ? Convert.ToInt32(item.amount * (config.xp_settings.night_settings.night_skinning_yield_modifier - 1)) : 0;

                if (bd.buff_values.ContainsKey(Buff.Skinning_Yield))
                {
                    if (Interface.CallHook("STCanReceiveYield", player, dispenser.baseEntity) == null)
                    {
                        var amount = item.amount * (bd.buff_values[Buff.Skinning_Yield] * gather_modifier) + (TOD_Sky.Instance.IsNight ? item.amount * (config.xp_settings.night_settings.night_skinning_yield_modifier - 1) : 0);
                        if (amount > 0.5)
                        {
                            if (amount < 1) amount = 1;
                            item.amount += Convert.ToInt32(amount);
                        }
                    }
                }

                if (!bonus && bd.buff_values.ContainsKey(Buff.Instant_Skin) && RollSuccessful((bd.buff_values[Buff.Instant_Skin] * gather_modifier)))
                {
                    foreach (var r in dispenser.containedItems)
                    {
                        if (r.amount < 1) continue;
                        var bonus_amount = Convert.ToInt32(r.amount + (r.amount * (TOD_Sky.Instance.IsNight ? config.xp_settings.night_settings.night_skinning_yield_modifier - 1 : 0)) + (r.amount * (bd.buff_values.ContainsKey(Buff.Skinning_Yield) ? bd.buff_values[Buff.Skinning_Yield] : 0)));
                        if (r.itemDef.shortname == item.info.shortname) item.amount += bonus_amount;
                        else
                        {
                            //player.GiveItem(ItemManager.CreateByName(r.itemDef.shortname, amount_to_give));
                            if (bonus_amount > 0) GiveItem(player, ItemManager.CreateByName(r.itemDef.shortname, bonus_amount));
                        }

                        r.amount = 0;
                    }
                }
                if (bd.buff_values.ContainsKey(Buff.Skin_Cook) && RollSuccessful((bd.buff_values[Buff.Skin_Cook] * gather_modifier)))
                {
                    var cooked = GetCookedMeat(item.info.shortname);
                    if (!string.IsNullOrEmpty(cooked))
                    {
                        GiveItem(player, ItemManager.CreateByName(cooked, item.amount));
                        //player.GiveItem(ItemManager.CreateByName(cooked, item.amount));
                        item.amount = 0;
                        item.Remove();
                    }
                }

                if (bonus && bd.buff_values.ContainsKey(Buff.Skinning_Luck) && RollSuccessful((bd.buff_values[Buff.Skinning_Luck] * gather_modifier)) && config.loot_settings.skinning_loot_table.Count > 0)
                {
                    var randProfile = RollLootItem(config.loot_settings.skinning_loot_table);
                    if (randProfile != null)
                    {
                        var randomitem = CreateDropItem(randProfile);
                        if (randomitem != null) player.GiveItem(randomitem);
                        if (NotificationsOn(player)) PrintToChat(player, string.Format(lang.GetMessage("ItemFound", this, player.UserIDString), randomitem.amount, randomitem.name ?? randomitem.info.displayName.english, "corpse"));
                    }
                    //if (def != null) player.GiveItem(ItemManager.CreateByName(randomitem.Key, quantity));                    
                }
            }
            return null;
        }

        void HandleSmeltOnMine(BasePlayer player, Item item)
        {
            var refined = GetRefinedMaterial(item.info.shortname);
            if (!string.IsNullOrEmpty(refined))
            {
                GiveItem(player, ItemManager.CreateByName(refined, Math.Max(item.amount, 1)));
                item.amount = 0;
                item.Remove();
            }
        }

        void HandleInstantMining(ResourceDispenser dispenser, BasePlayer player, Item item, BuffDetails bd)
        {
            Interface.CallHook("STOnInstantMineTrigger", player, dispenser, item);
            foreach (var r in dispenser.containedItems)
            {
                if (r.amount < 1) continue;
                var bonus_amount = Convert.ToInt32(r.amount + (r.amount * (TOD_Sky.Instance.IsNight ? config.xp_settings.night_settings.night_mining_yield_modifier - 1 : 0)) + (r.amount * (bd.buff_values.ContainsKey(Buff.Mining_Yield) ? bd.buff_values[Buff.Mining_Yield] : 0)));
                if (r.itemDef.shortname == item.info.shortname) item.amount += bonus_amount;
                else
                {
                    //player.GiveItem(ItemManager.CreateByName(r.itemDef.shortname, amount_to_give));
                    if (bonus_amount > 0) GiveItem(player, ItemManager.CreateByName(r.itemDef.shortname, bonus_amount));
                }

                r.amount = 0;
            }
        }

        void HandleMiningYield(ResourceDispenser dispenser, BasePlayer player, Item item, BuffDetails bd, float gather_modifier)
        {
            if (Interface.CallHook("STCanReceiveYield", player, dispenser.baseEntity) == null)
            {
                var amount = item.amount * (bd.buff_values[Buff.Mining_Yield] * gather_modifier) + (TOD_Sky.Instance.IsNight ? item.amount * (config.xp_settings.night_settings.night_mining_yield_modifier - 1) : 0);
                if (amount > 0.5)
                {
                    if (amount < 1) amount = 1;
                    item.amount += Convert.ToInt32(amount);
                }
            }
        }

        string GetCookedMeat(string shortname)
        {
            switch (shortname)
            {
                case "bearmeat": return "bearmeat.cooked";
                case "chicken.raw": return "chicken.cooked";
                case "deermeat.raw": return "deermeat.cooked";
                case "fish.raw": return "fish.cooked";
                case "horsemeat.raw": return "horsemeat.cooked";
                case "humanmeat.raw": return "humanmeat.cooked";
                case "meat.boar": return "meat.pork.cooked";
                case "wolfmeat.raw": return "wolfmeat.cooked";
                default: return null;
            }
        }

        string GetRefinedMaterial(string shortname)
        {
            switch (shortname)
            {
                case "hq.metal.ore": return "metal.refined";
                case "metal.ore": return "metal.fragments";
                case "sulfur.ore": return "sulfur";
                default: return null;
            }
        }

        void HandleTree(BasePlayer player, ResourceDispenser dispenser)
        {
            var heldEntity = player.GetHeldEntity();
            if (heldEntity == null || !(heldEntity is AttackEntity)) return;
            dispenser.AssignFinishBonus(player, 1f, heldEntity as AttackEntity);
            HitInfo hitInfo = new HitInfo(player, dispenser.baseEntity, Rust.DamageType.Generic, dispenser.baseEntity.MaxHealth(), dispenser.transform.position);
            hitInfo.gatherScale = 0f;
            hitInfo.PointStart = dispenser.transform.position;
            hitInfo.PointEnd = dispenser.transform.position;
            hitInfo.WeaponPrefab = heldEntity;
            hitInfo.Weapon = null;
            dispenser.baseEntity.OnAttacked(hitInfo);
        }

        public static bool UltimateTriggered = false;

        LootItems RollLootItem(List<LootItems> items)
        {
            var count = 0;
            foreach (var entry in items)
                count += entry.dropWeight;

            var roll = UnityEngine.Random.Range(0, count + 1);
            var _checked = 0;

            foreach (var entry in items)
            {
                _checked += entry.dropWeight;
                if (roll <= _checked) return entry;
            }

            return items.GetRandom();
        }

        Item CreateDropItem(LootItems info)
        {
            var item = ItemManager.CreateByName(info.shortname, Math.Max(UnityEngine.Random.Range(info.min, info.max + 1), 1), info.skin);
            if (item == null) return null;
            if (!string.IsNullOrEmpty(info.displayName)) item.name = info.displayName;
            return item;
        }

        object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || item == null || player == null || player.IsNpc || !player.userID.IsSteamId()) return null;
            BuffDetails bd;
            if (!buffDetails.TryGetValue(player.userID, out bd))
            {
                //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {player.displayName}[{player.UserIDString}] - OnDispenserBonus. [Online = {player.IsConnected}]", this, true);
                if (player.IsConnected) PrintToChat(player, lang.GetMessage("FailReload", this, player.UserIDString));
                return null;
            }
                
            HeldEntity heldEntity = player.GetHeldEntity();
            var gather_modifier = 1f;
            if (dispenser.gatherType == ResourceDispenser.GatherType.Tree)
            {
                float value;
                if (heldEntity != null && heldEntity is Chainsaw) gather_modifier = config.tools_black_white_list_settings.power_tool_modifier.woodcutting_perk_modifier;
                if (!UltimateTriggered || (config.ultimate_settings.ultimate_woodcutting.award_xp)) AwardXP(player, config.xp_settings.xp_sources.TreeHitFinal * gather_modifier, dispenser.baseEntity);
                if (bd.buff_values.ContainsKey(Buff.Woodcutting_Luck) && RollSuccessful((bd.buff_values[Buff.Woodcutting_Luck] * gather_modifier)) && config.loot_settings.wc_loot_table.Count > 0)
                {
                    var randProfile = RollLootItem(config.loot_settings.wc_loot_table);
                    if (randProfile != null)
                    {
                        var randomitem = CreateDropItem(randProfile);
                        if (randomitem != null) player.GiveItem(randomitem);
                        if (NotificationsOn(player)) PrintToChat(player, string.Format(lang.GetMessage("ItemFound", this, player.UserIDString), randomitem.amount, randomitem.name ?? randomitem.info.displayName.english, "tree"));
                    }
                    //if (def != null) player.GiveItem(ItemManager.CreateByName(randomitem.Key, quantity));                    
                }
                if (bd.buff_values.TryGetValue(Buff.Regrowth, out value) && RollSuccessful(value * gather_modifier))
                {
                    var newTree = GameManager.server.CreateEntity(dispenser.baseEntity.PrefabName, dispenser.baseEntity.transform.position, dispenser.baseEntity.transform.rotation);
                    newTree.Spawn();
                    if (NotificationsOn(player)) PrintToChat(player, lang.GetMessage("RegrowthProc", this, player.UserIDString));
                }
                if (!UltimateTriggered && bd.buff_values.ContainsKey(Buff.Woodcutting_Ultimate) && IsUltimateEnabled(player, Buff.Woodcutting_Ultimate))
                {
                    if ((heldEntity != null && heldEntity is Chainsaw && config.tools_black_white_list_settings.power_tool_modifier.woodcutting_perk_modifier == 0)) goto noChainsaw;
                    float _time = Time.time;
                    List<ResourceDispenser> other_trees = Pool.GetList<ResourceDispenser>();
                    other_trees.AddRange(FindEntitiesOfType<BaseEntity>(dispenser.transform.position, config.ultimate_settings.ultimate_woodcutting.distance_from_player).Where(x => x.PrefabName.StartsWith("assets/bundled/prefabs/autospawn/resource/v3_") || x.PrefabName.StartsWith("assets/bundled/prefabs/autospawn/resource/swamp")).Select(x => x.GetComponent<ResourceDispenser>()));
                    UltimateTriggered = true;
                    foreach (var _dispenser in other_trees)
                    {
                        if (_dispenser == null) continue;
                        if (_dispenser != dispenser && _dispenser.gatherType == ResourceDispenser.GatherType.Tree)
                        {
                            HandleTree(player, _dispenser);
                        }
                    }
                    noChainsaw:
                    UltimateTriggered = false;
                }
            }
            else if (dispenser.gatherType == ResourceDispenser.GatherType.Ore)
            {
                if (heldEntity != null && heldEntity is Jackhammer) gather_modifier = config.tools_black_white_list_settings.power_tool_modifier.mining_perk_modifier;
                AwardXP(player, config.xp_settings.xp_sources.NodeHitFinal * gather_modifier, dispenser.baseEntity);
                if (bd.buff_values.ContainsKey(Buff.Mining_Luck) && RollSuccessful((bd.buff_values[Buff.Mining_Luck] * gather_modifier)) && config.loot_settings.mining_loot_table.Count > 0)
                {
                    var randProfile = RollLootItem(config.loot_settings.mining_loot_table);
                    if (randProfile != null)
                    {
                        var randomitem = CreateDropItem(randProfile);
                        if (randomitem != null) player.GiveItem(randomitem);
                        if (NotificationsOn(player)) PrintToChat(player, string.Format(lang.GetMessage("ItemFound", this, player.UserIDString), randomitem.amount, randomitem.name ?? randomitem.info.displayName.english, "node"));
                    }
                    //if (def != null) player.GiveItem(ItemManager.CreateByName(randomitem.Key, quantity));                    
                }
            }
            // Dispenser bonus doesnt trigger for flesh.
            HandleDispenser(dispenser, player, item, true);
            return null;
        }        

        object OnItemRepair(BasePlayer player, Item item)
        {
            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd))
            {
                if (bd.buff_values.ContainsKey(Buff.MaxRepair))
                {
                    NextTick(() =>
                    {
                        item.maxCondition = ItemDefs[item.info.shortname].condition.max;
                        item.condition = item.maxCondition;
                    });                    
                }
            }
            return null;
        }

        void OnLoseCondition(Item item, ref float amount)
        {
            var player = item.GetOwnerPlayer();
            if (player != null && !player.IsNpc && player.userID.IsSteamId())
            {
                BuffDetails bd;
                if (buffDetails.TryGetValue(player.userID, out bd))
                {
                    float amount_to_repair = 0f;
                    float value;
                    if (bd.buff_values.TryGetValue(Buff.Durability, out value)) 
                        amount_to_repair += (amount * value);

                    if (bd.buff_values.ContainsKey(Buff.Primitive_Expert) && config.buff_settings.primitive_weapons.Contains(item.info.shortname)) 
                        amount_to_repair = amount;

                    else if (bd.buff_values.TryGetValue(Buff.Woodcutting_Tool_Durability, out value) && config.tools_black_white_list_settings.wc_tools.Contains(item.info.shortname))
                        amount_to_repair += (value * amount);

                    else if (bd.buff_values.TryGetValue(Buff.Mining_Tool_Durability, out value) && config.tools_black_white_list_settings.mining_tools.Contains(item.info.shortname))
                        amount_to_repair += (value * amount);

                    else if (bd.buff_values.TryGetValue(Buff.Skinning_Tool_Durability, out value) && config.tools_black_white_list_settings.skinning_tools.Contains(item.info.shortname))
                        amount_to_repair += (value * amount);

                    item.condition += amount_to_repair >= amount ? amount : amount_to_repair;
                }
            }
        }
        
        void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
        {
            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd))
            {
                if (bd.buff_values.ContainsKey(Buff.Free_Bullet_Chance) && RollSuccessful(bd.buff_values[Buff.Free_Bullet_Chance]))
                {
                    var heldEntity = projectile.GetItem();
                    if (heldEntity == null) return;
                    projectile.primaryMagazine.contents++;
                    projectile.SendNetworkUpdateImmediate();
                }
            }
        }

        void CanCatchFish(BasePlayer player, BaseFishingRod fishingRod, Item fish)
        {
            if (player.IsNpc || !player.userID.IsSteamId()) return;
            AwardXP(player, config.xp_settings.xp_sources.FishCaught);

            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.ContainsKey(Buff.Extra_Fish))
            {

                var roll = UnityEngine.Random.Range(0f, 100f);
                if (roll > 100f - (bd.buff_values[Buff.Extra_Fish] / 1 * 100))
                {
                    var extra = UnityEngine.Random.Range(1, fish.amount);
                    if (NotificationsOn(player)) PrintToChat(player, string.Format(lang.GetMessage("ExtraFish", this, player.UserIDString), extra, fish.info.displayName.english));
                    //NextTick(() => player?.GiveItem(ItemManager.CreateByName(fish.info.shortname, extra, fish.skin)));
                    NextTick(() => GiveItem(player, ItemManager.CreateByName(fish.info.shortname, extra, fish.skin)));
                }
            }
        }

        void OnFishCatch(Item item, BaseFishingRod rod, BasePlayer player)
        {
            BuffDetails bd;
            float luck;
            if (buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.TryGetValue(Buff.Fishing_Luck, out luck) && RollSuccessful(luck))
            {
                var randProfile = RollLootItem(config.loot_settings.fishing_loot_table);
                if (randProfile != null)
                {
                    var randomitem = CreateDropItem(randProfile);
                    if (randomitem != null) player.GiveItem(randomitem);
                    if (NotificationsOn(player)) PrintToChat(player, string.Format(lang.GetMessage("ItemFound", this, player.UserIDString), randomitem.amount, randomitem.name ?? randomitem.info.displayName.english, item.info.displayName.english));
                }
            }
        }

        bool CheckHarvestingBlacklist = false;

        void OnCollectiblePickup(CollectibleEntity entity, BasePlayer player)
        {
            if (player == null || entity == null || player.IsNpc || !player.userID.IsSteamId() || entity.itemList == null) return;
            AwardXP(player, config.xp_settings.xp_sources.CollectWildPlant, entity);
            BuffDetails bd;
            float buffValue;
            if (buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.TryGetValue(Buff.Harvest_Wild_Yield, out buffValue) && Interface.CallHook("STCanReceiveYield", player, entity) == null)
            {
                foreach (var item in entity.itemList)
                {
                    if (item == null) continue;
                    if (CheckHarvestingBlacklist && config.buff_settings.harvest_yield_blacklist.Contains(item.itemDef.shortname)) continue;
                    item.amount += Convert.ToInt32(Math.Round((buffValue * item.amount) + (TOD_Sky.Instance.IsNight ? (config.xp_settings.night_settings.night_harvesting_yield_modifier - 1) * item.amount : 0), 0, MidpointRounding.AwayFromZero));
                }
            }
            else
            {
                foreach (var item in entity.itemList)
                {
                    if (item == null) continue;
                    item.amount += Convert.ToInt32(Math.Round(TOD_Sky.Instance.IsNight ? (config.xp_settings.night_settings.night_harvesting_yield_modifier - 1) * item.amount : 0, 0, MidpointRounding.AwayFromZero));
                }
            }
            if (HasAnimalBuff(player, AnimalBuff.Boar) && !string.IsNullOrEmpty(entity.PrefabName) && (entity.PrefabName.StartsWith("assets/content/nature/plants/mushroom/") || entity.PrefabName.StartsWith("assets/bundled/prefabs/autospawn/collectable/"))) RollBoarLoot(player, entity);
        }

        Dictionary<ulong, float> Harvester_Ultimate_cooldown = new Dictionary<ulong, float>();

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            var player = plan?.GetOwnerPlayer();
            if (player == null) return;
            var entity = go?.ToBaseEntity();
            if (entity != null)
            {
                if (entity is BuildingBlock) AwardXP(player, config.xp_settings.xp_sources.BuildingBlockDeployed, entity);
            }

            GrowableEntity plant = go.GetComponent<GrowableEntity>();
            if (plant != null)
            {
                BuffDetails bd;
                PlayerInfo pi;
                if (buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.ContainsKey(Buff.Harvester_Ultimate) && IsUltimateEnabled(player, Buff.Harvester_Ultimate) && pcdData.pEntity.TryGetValue(player.userID, out pi))
                {
                    if (config.ultimate_settings.ultimate_harvesting.cooldown > 0)
                    {
                        float cd;
                        if (!Harvester_Ultimate_cooldown.TryGetValue(player.userID, out cd))
                        {
                            Harvester_Ultimate_cooldown.Add(player.userID, Time.time + config.ultimate_settings.ultimate_harvesting.cooldown);
                            if (config.ultimate_settings.ultimate_harvesting.notify_on_cooldown) PrintToChat(player, string.Format(lang.GetMessage("HarvestUltiCDNotification", this, player.UserIDString), config.ultimate_settings.ultimate_harvesting.cooldown));
                        }

                        else
                        {
                            if (Time.time < cd) return;
                            else
                            {
                                Harvester_Ultimate_cooldown[player.userID] = Time.time + config.ultimate_settings.ultimate_harvesting.cooldown;
                                if (config.ultimate_settings.ultimate_harvesting.notify_on_cooldown) PrintToChat(player, string.Format(lang.GetMessage("HarvestUltiCDNotification", this, player.UserIDString), config.ultimate_settings.ultimate_harvesting.cooldown));
                            }
                        }
                    }
                    var genes = plant.Genes.Genes;
                    for (int i = 0; i < pi.plant_genes.Length; i++)
                    {
                        switch (pi.plant_genes[i])
                        {
                            case 'g':
                                genes[i].Set(GrowableGenetics.GeneType.GrowthSpeed);
                                genes[i].SetPrevious(GrowableGenetics.GeneType.GrowthSpeed);
                                break;

                            case 'e':
                                genes[i].Set(GrowableGenetics.GeneType.Empty);
                                genes[i].SetPrevious(GrowableGenetics.GeneType.Empty);
                                break;

                            case 'x':
                                genes[i].Set(GrowableGenetics.GeneType.Empty);
                                genes[i].SetPrevious(GrowableGenetics.GeneType.Empty);
                                break;

                            case 'w':
                                genes[i].Set(GrowableGenetics.GeneType.WaterRequirement);
                                genes[i].SetPrevious(GrowableGenetics.GeneType.WaterRequirement);
                                break;

                            case 'y':
                                genes[i].Set(GrowableGenetics.GeneType.Yield);
                                genes[i].SetPrevious(GrowableGenetics.GeneType.Yield);
                                break;

                            case 'h':
                                genes[i].Set(GrowableGenetics.GeneType.Hardiness);
                                genes[i].SetPrevious(GrowableGenetics.GeneType.Hardiness);
                                break;

                            default:
                                genes[i].Set(GrowableGenetics.GeneType.GrowthSpeed);
                                genes[i].SetPrevious(GrowableGenetics.GeneType.GrowthSpeed);
                                break;
                        }
                    }
                    plant.SendNetworkUpdateImmediate();
                }
            }
        }
        
        object OnItemCraftFinished(ItemCraftTask task, Item item)
        {
            var player = task.owner;
            if (player == null) return null;
            if (task.blueprint == null)
            {
                return null;
            }

            var experienceGain = CraftTimes.ContainsKey(task.taskUID) ? Math.Round(CraftTimes[task.taskUID] * config.xp_settings.xp_sources.Crafting, 2) : Math.Round((task.blueprint.time + 0.99f) * config.xp_settings.xp_sources.Crafting, 2);            
            AwardXP(player, experienceGain);
            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd))
            {
                if (bd.buff_values.ContainsKey(Buff.Craft_Refund) && RollSuccessful(bd.buff_values[Buff.Craft_Refund]))
                {
                    var refunded = 0;
                    ItemBlueprint bp;
                    if (item_BPs.TryGetValue(item.info.shortname, out bp))
                    {
                        foreach (var component in bp.ingredients)
                        {
                            if (config.tools_black_white_list_settings.craft_refund_blacklist.Contains(component.itemDef.shortname)) continue;
                            var nitem = ItemManager.CreateByName(component.itemDef.shortname, Convert.ToInt32(component.amount));
                            if (nitem == null) continue;
                            if (!player.inventory.containerBelt.IsFull() || !player.inventory.containerMain.IsFull())
                            {
                                GiveItem(player, nitem);
                                //player.inventory.GiveItem(nitem);
                            }
                            else nitem.DropAndTossUpwards(player.transform.position);
                            refunded++;
                        }
                        if (refunded > 0 && NotificationsOn(player)) PrintToChat(player, lang.GetMessage("CraftRefund", this, player.UserIDString));
                    }                    
                }
                if (bd.buff_values.ContainsKey(Buff.Craft_Duplicate) && RollSuccessful(bd.buff_values[Buff.Craft_Duplicate]) && !config.tools_black_white_list_settings.craft_duplicate_blacklist.Contains(item.info.shortname))
                {
                    var ditem = ItemManager.CreateByName(item.info.shortname, item.amount, item.skin);
                    if (ditem != null)
                    {
                        if (NotificationsOn(player)) PrintToChat(player, string.Format(lang.GetMessage("DuplicateProc", this, player.UserIDString), item.info.displayName.english));
                        player.GiveItem(ditem);
                    }                    
                }
            }
            
            if (task.amount > 0)
            {
                if (CraftTimes.ContainsKey(task.taskUID)) CraftTimes[task.taskUID] = GetModifiedTime(player, task);
                return null;
            }

            CraftTimes.Remove(task.taskUID);

            if (task.blueprint != null && task.blueprint.name.Contains("(Clone)"))
            {
                var behaviours = task.blueprint.GetComponents<MonoBehaviour>();
                foreach (var behaviour in behaviours)
                {
                    if (behaviour.name.Contains("(Clone)")) UnityEngine.Object.Destroy(behaviour);
                }
            }
            return null;
        }

        object OnItemCraft(ItemCraftTask task, BasePlayer player)
        {
            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd))
            {
                if (bd.buff_values.ContainsKey(Buff.Craft_Speed))
                {
                    var craftingTime = task.blueprint.time;
                    var reducedTime = craftingTime - (craftingTime * bd.buff_values[Buff.Craft_Speed]);
                    if (reducedTime <= 0) reducedTime = 0.0f;
                    if (!task.blueprint.name.Contains("(Clone)"))
                        task.blueprint = UnityEngine.Object.Instantiate(task.blueprint);
                    task.blueprint.time = reducedTime;

                }
                if (!CraftTimes.ContainsKey(task.taskUID)) CraftTimes.Add(task.taskUID, GetModifiedTime(player, task));
            }

            return null;
        }

        Dictionary<int, float> CraftTimes = new Dictionary<int, float>();


        float GetModifiedTime(BasePlayer player, ItemCraftTask task)
        {
            var workbenchLevel = player.currentCraftLevel;

            if (workbenchLevel == 0) return task.blueprint.time;
            var diff = workbenchLevel - task.blueprint.workbenchLevelRequired;
            if (diff < 0.5) return task.blueprint.time;
            else if (diff < 1.5) return task.blueprint.time / 2;
            else return task.blueprint.time / 4;
        }

        void OnGrowableGathered(GrowableEntity plant, Item item, BasePlayer player)
        {
            if (player.IsNpc || !player.userID.IsSteamId()) return;
            if (!item.info.shortname.Contains("seed") && (!config.xp_settings.ripe_required || plant.State == PlantProperties.State.Ripe)) AwardXP(player, config.xp_settings.xp_sources.CollectGrownPlant, plant);
            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.ContainsKey(Buff.Harvest_Grown_Yield) && Interface.CallHook("STCanReceiveYield", player, plant) == null)
            {
                var quantity = Convert.ToInt32(Math.Round(bd.buff_values[Buff.Harvest_Grown_Yield] * item.amount + (TOD_Sky.Instance.IsNight && config.xp_settings.night_settings.include_grown_harvesting ? (config.xp_settings.night_settings.night_harvesting_yield_modifier - 1) * item.amount : 0), 0, MidpointRounding.AwayFromZero));
                item.amount += quantity;
            }
            else item.amount += Convert.ToInt32(Math.Round(TOD_Sky.Instance.IsNight && config.xp_settings.night_settings.include_grown_harvesting ? (config.xp_settings.night_settings.night_harvesting_yield_modifier - 1) * item.amount : 0, 0, MidpointRounding.AwayFromZero));
        }

        object OnEntityTakeDamage(BaseAnimalNPC animal, HitInfo info)
        {
            if (animal == null || info == null || info.InitiatorPlayer == null || info.InitiatorPlayer.IsNpc || !info.InitiatorPlayer.userID.IsSteamId()) return null;

            BuffDetails bd;
            if (!buffDetails.TryGetValue(info.InitiatorPlayer.userID, out bd))
            {
                //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {info.InitiatorPlayer.displayName}[{info.InitiatorPlayer.UserIDString}] - OnEntityTakeDamage(BaseAnimalNPC animal, HitInfo info). [Online = {info.InitiatorPlayer.IsConnected}]", this, true);
                if (info.InitiatorPlayer.IsConnected) PrintToChat(info.InitiatorPlayer, lang.GetMessage("FailReload", this, info.InitiatorPlayer.UserIDString));
                return null;
            }

            float value;
            if (bd.buff_values.TryGetValue(Buff.Animal_NPC_Damage, out value))
            {
                info.damageTypes.ScaleAll(1f + value);
            }

            if (config.ultimate_settings.ultimate_combat.animals_enabled && bd.buff_values.ContainsKey(Buff.Combat_Ultimate) && IsUltimateEnabled(info.InitiatorPlayer, Buff.Combat_Ultimate))
            {
                info.InitiatorPlayer.Heal(info.damageTypes.Total() * config.ultimate_settings.ultimate_combat.health_scale);
            }

            return null;
        }

        object OnEntityTakeDamage(SimpleShark shark, HitInfo info)
        {
            if (info.InitiatorPlayer == null || info.InitiatorPlayer.IsNpc || !info.InitiatorPlayer.userID.IsSteamId()) return null;
            BuffDetails bd;
            if (!buffDetails.TryGetValue(info.InitiatorPlayer.userID, out bd)) return null;

            float value;
            if (bd.buff_values.TryGetValue(Buff.UnderwaterDamageBonus, out value) && IsUnderwater(info.InitiatorPlayer))
            {
                info.damageTypes.ScaleAll(1f + value);
            }

            return null;
        }


        object OnEntityTakeDamage(BaseVehicle vehicle, HitInfo info)
        {
            if (info == null || info.damageTypes == null) return null;
            var player = vehicle.GetDriver();
            if (player == null) return null;
            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.ContainsKey(Buff.Vehicle_Ultimate))
            {
                info.damageTypes.ScaleAll(1f - config.ultimate_settings.ultimate_vehicle.reduce_by);
            }
            return null;
        }

        public static bool ContainsTopology(TerrainTopology.Enum mask, Vector3 position)
        {
            return (TerrainMeta.TopologyMap.GetTopology(position, (int)mask));
        }

        public bool IsUnderwater(BasePlayer player)
        {
            return player.WaterFactor() == 1f || ContainsTopology(TerrainTopology.Enum.Monument, player.transform.position) && ContainsTopology(TerrainTopology.Enum.Ocean, player.transform.position);
        }

        object OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null) return null;
            BuffDetails bd;
            if ((player.IsNpc || !player.userID.IsSteamId()) && info.InitiatorPlayer != null)
            {
                var attackerPlayer = info.InitiatorPlayer;
                if (attackerPlayer.IsNpc || !attackerPlayer.userID.IsSteamId()) return null;
                //Confirmed attacker is real.
                if (!buffDetails.TryGetValue(attackerPlayer.userID, out bd))
                {
                    //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {attackerPlayer.displayName}[{attackerPlayer.UserIDString}] - OnEntityTakeDamage(BasePlayer player, HitInfo info) (Damage to NPC). [Online = {attackerPlayer.IsConnected}]", this, true);
                    if (attackerPlayer.IsConnected) PrintToChat(attackerPlayer, lang.GetMessage("FailReload", this, attackerPlayer.UserIDString));
                    return null;
                }
                if (bd.buff_values.ContainsKey(Buff.Human_NPC_Damage))
                {
                    var scale = bd.buff_values[Buff.Human_NPC_Damage];
                    info?.damageTypes?.ScaleAll(1f + scale);
                }
                if (config.ultimate_settings.ultimate_combat.scientists_enabled && bd.buff_values.ContainsKey(Buff.Combat_Ultimate) && IsUltimateEnabled(info.InitiatorPlayer, Buff.Combat_Ultimate))
                {
                    attackerPlayer.Heal(info.damageTypes.Total() * config.ultimate_settings.ultimate_combat.health_scale);
                }
                if (bd.buff_values.ContainsKey(Buff.UnderwaterDamageBonus) && IsUnderwater(player))
                {
                    info.damageTypes.ScaleAll(1f + bd.buff_values[Buff.UnderwaterDamageBonus]);
                }
                return null;
            }
            if (!buffDetails.TryGetValue(player.userID, out bd))
            {
                //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {player.displayName}[{player.UserIDString}] - OnEntityTakeDamage(BasePlayer player, HitInfo info) (Damage to player). [Online = {player.IsConnected}]", this, true);
                if (player.IsConnected) PrintToChat(player, lang.GetMessage("FailReload", this, player.UserIDString));
                return null;
            }
            if (bd == null) return null;
            var damage = info?.damageTypes?.GetMajorityDamageType();
            switch (damage)
            {
                case Rust.DamageType.Thirst:
                case Rust.DamageType.Hunger:
                    AddRegenDelay(player);
                    return null;
                case Rust.DamageType.Cold:
                case Rust.DamageType.ColdExposure:
                    if (bd.buff_values.ContainsKey(Buff.No_Cold_Damage))
                    {
                        player.metabolism.temperature.SetValue(20f);
                        return true;
                    }
                    else AddRegenDelay(player);
                    return null;
                case Rust.DamageType.Radiation:
                case Rust.DamageType.RadiationExposure:
                    if (bd.buff_values.ContainsKey(Buff.Radiation_Reduction))
                    {
                        var reducedValue = 1f - bd.buff_values[Buff.Radiation_Reduction];
                        if (reducedValue < 0) reducedValue = 0;
                        if (reducedValue == 0)
                        {
                            player.metabolism.radiation_level.SetValue(0);
                            player.metabolism.radiation_poison.SetValue(0);
                        }
                        info.damageTypes.ScaleAll(reducedValue);
                        if (reducedValue > 0) AddRegenDelay(player);
                    }
                    else AddRegenDelay(player);
                    return null;
                case Rust.DamageType.Heat:
                    if (bd.buff_values.ContainsKey(Buff.Fire_Damage_Reduction))
                    {
                        var reducedValue = 1f - bd.buff_values[Buff.Fire_Damage_Reduction];
                        if (reducedValue < 0) reducedValue = 0;
                        info.damageTypes.ScaleAll(reducedValue);
                        if (reducedValue > 0) AddRegenDelay(player);
                    }
                    else AddRegenDelay(player);

                    return null;
                case Rust.DamageType.Fall:
                    if (HasAnimalBuff(player, AnimalBuff.Chicken)) info.damageTypes.ScaleAll(0f);
                    else if (bd.buff_values.ContainsKey(Buff.Fall_Damage_Reduction))
                    {
                        var reducedValue = 1f - bd.buff_values[Buff.Fall_Damage_Reduction];
                        if (reducedValue < 0) reducedValue = 0;
                        if (reducedValue > 0) AddRegenDelay(player);
                        info.damageTypes.ScaleAll(reducedValue);
                    }
                    else AddRegenDelay(player);
                    HandleBearBuff(player, info, Rust.DamageType.Fall);
                    return null;
            }
            var attacker = info.Initiator;
            if (attacker == null) return null;
            if (bd.buff_values.ContainsKey(Buff.Animal_Damage_Resist) && config.buff_settings.animals.Contains(attacker.ShortPrefabName))
            {
                var reducedValue = 1f - bd.buff_values[Buff.Animal_Damage_Resist];
                if (reducedValue < 0) reducedValue = 0;
                info.damageTypes.ScaleAll(reducedValue);
                if (reducedValue > 0) AddRegenDelay(player);
                HandleBearBuff(player, info, info.damageTypes.GetMajorityDamageType());
                return null;
            }
            if (bd.buff_values.ContainsKey(Buff.SharkResistance) && attacker is SimpleShark)
            {
                var reducedValue = 1f - bd.buff_values[Buff.SharkResistance];
                if (reducedValue < 0) reducedValue = 0;
                info.damageTypes.ScaleAll(reducedValue);
                if (reducedValue > 0) AddRegenDelay(player);
                HandleBearBuff(player, info, info.damageTypes.GetMajorityDamageType());
                return null;
            }
            var damageScale = 1f;
            var player_attacker = attacker as BasePlayer;
            if (player_attacker != null)
            {
                if (bd.buff_values.ContainsKey(Buff.Melee_Resist))
                {
                    var heldEntity = player_attacker.GetHeldEntity();
                    if (heldEntity != null && heldEntity is BaseMelee)
                    {
                        damageScale -= bd.buff_values[Buff.Melee_Resist];
                    }
                }

                if (bd.buff_values.ContainsKey(Buff.PVP_Shield))
                {
                    damageScale -= bd.buff_values[Buff.PVP_Shield];
                }
                BuffDetails abd;
                if (buffDetails.TryGetValue(player_attacker.userID, out abd))
                {
                    if (abd.buff_values.ContainsKey(Buff.PVP_Critical) && RollSuccessful(abd.buff_values[Buff.PVP_Critical]))
                    {
                        damageScale += UnityEngine.Random.Range(0.01f, config.buff_settings.pvp_critical_modifier);
                    }
                    if (abd.buff_values.ContainsKey(Buff.PVP_Damage))
                    {
                        damageScale += abd.buff_values[Buff.PVP_Damage];
                    }

                    if (config.ultimate_settings.ultimate_combat.players_enabled && abd.buff_values.ContainsKey(Buff.Combat_Ultimate) && IsUltimateEnabled(info.InitiatorPlayer, Buff.Combat_Ultimate))
                    {
                        player_attacker.Heal(info.damageTypes.Total() * config.ultimate_settings.ultimate_combat.health_scale);
                    }
                }

            }
            var npc_attacker = attacker as ScientistNPC;
            if (npc_attacker != null)
            {
                if (bd.buff_values.ContainsKey(Buff.Human_NPC_Defence))
                {
                    damageScale -= bd.buff_values[Buff.Human_NPC_Defence];
                }
            }
            if (damageScale < 0) damageScale = 0;
            if (damageScale != 1f) info.damageTypes.ScaleAll(damageScale);
            if (damageScale > 0) AddRegenDelay(player);
            HandleBearBuff(player, info, info.damageTypes.GetMajorityDamageType());
            return null;
        }



        void HandleBearBuff(BasePlayer player, HitInfo info, Rust.DamageType damageType)
        {
            if (HasAnimalBuff(player, AnimalBuff.PolarBear) && OverShields.ContainsKey(player))
            {
                var shield_value = OverShields[player];
                var total_Damage = info.damageTypes.Total();
                if (shield_value <= 0) RemoveAnimalBuff(player);
                else if (total_Damage <= shield_value)
                {
                    OverShields[player] = shield_value - total_Damage;
                    info.damageTypes.ScaleAll(0f);
                    Overshield_main(player, shield_value - total_Damage);
                }
                else
                {
                    var excess_damage = total_Damage - shield_value;
                    Unsubscribe("OnEntityTakeDamage");
                    player.Hurt(excess_damage, damageType);
                    Subscribe("OnEntityTakeDamage");
                    info.damageTypes.ScaleAll(0f);
                    RemoveAnimalBuff(player);
                }
            }
        }

        private void OnRecyclerToggle(Recycler recycler, BasePlayer player)
        {
            if (recycler.IsOn()) return;
            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.ContainsKey(Buff.Recycler_Speed))
            {
                float speedDecrease = bd.buff_values[Buff.Recycler_Speed];
                recycler.CancelInvoke(nameof(recycler.RecycleThink));
                var recycler_speed = 5.0f - speedDecrease;
                if (recycler_speed <= 0.1) recycler_speed = 0.1f;
                timer.Once(0.1f, () => recycler.InvokeRepeating(recycler.RecycleThink, recycler_speed - 0.1f, recycler_speed));
            }
        }

        List<LootContainer> looted_crates = new List<LootContainer>();
        void OnLootEntity(BasePlayer player, LootContainer entity)
        {
            switch (entity.ShortPrefabName)
            {
                case "codelockedhackablecrate_oilrig":
                case "codelockedhackablecrate":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.LootHackedCrate, entity);
                    }
                    break;
                case "heli_crate":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.LootHeliCrate, entity);
                    }
                    break;
                case "bradley_crate":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.LootBradleyCrate, entity);
                    }
                    break;
                case "crate_basic":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.crate_basic, entity);
                    }
                    break;
                case "crate_elite":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.crate_elite, entity);
                    }
                    break;
                case "crate_mine":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.crate_mine, entity);
                    }
                    break;
                case "crate_normal":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.crate_normal, entity);
                    }
                    break;
                case "crate_normal_2":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.crate_normal_2, entity);
                    }
                    break;
                case "crate_normal_2_food":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.crate_normal_2_food, entity);
                    }
                    break;
                case "crate_normal_2_medical":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.crate_normal_2_medical, entity);
                    }
                    break;
                case "crate_tools":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.crate_tools, entity);
                    }
                    break;
                case "crate_underwater_advanced":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.crate_underwater_advanced, entity);
                    }
                    break;
                case "crate_underwater_basic":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.crate_underwater_basic, entity);
                    }
                    break;
                case "crate_ammunition":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.crate_ammunition, entity);
                    }
                    break;
                case "crate_food_1":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.crate_food_1, entity);
                    }
                    break;
                case "crate_food_2":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.crate_food_2, entity);
                    }
                    break;
                case "crate_fuel":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.crate_fuel, entity);
                    }
                    break;
                case "crate_medical":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.crate_medical, entity);
                    }
                    break;

                case "trash-pile-1":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.crate_food_1, entity);
                    }
                    break;
                case "supply_drop":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.supply_drop, entity);
                    }
                    break;
                case "vehicle_parts":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.crate_basic, entity);
                    }
                    break;
                case "minecart":
                    if (!looted_crates.Contains(entity))
                    {
                        looted_crates.Add(entity);
                        AwardXP(player, config.xp_settings.xp_sources.crate_normal, entity);
                    }
                    break;
            }
        }

        bool IsRoadsign(string prefab)
        {
            switch (prefab)
            {
                case "roadsign1": return true;
                case "roadsign2": return true;
                case "roadsign3": return true;
                case "roadsign4": return true;
                case "roadsign5": return true;
                case "roadsign6": return true;
                case "roadsign7": return true;
                case "roadsign8": return true;
                case "roadsign9": return true;
            }
            return false;
        }

        object OnEntityTakeDamage(LootContainer entity, HitInfo info)
        {
            var player = info?.InitiatorPlayer;
            if (player == null || entity.ShortPrefabName == "trash-pile-1") return null;

            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd))
            {
                if (bd.buff_values.ContainsKey(Buff.Barrel_Smasher) && !IsRoadsign(entity.ShortPrefabName)) info?.damageTypes?.ScaleAll(100f);

                if (info?.damageTypes?.Total() >= entity?.health)
                {
                    if (bd.buff_values.ContainsKey(Buff.Extra_Scrap_Barrel) && IsBarrel(entity.ShortPrefabName) && RollSuccessful(bd.buff_values[Buff.Extra_Scrap_Barrel]))
                    {
                        entity.inventory.capacity++;
                        var item = ItemManager.CreateByName("scrap", UnityEngine.Random.Range(config.buff_settings.min_extra_scrap, config.buff_settings.max_extra_scrap + 1));
                        if (!item.MoveToContainer(entity.inventory)) item.Remove();
                    }
                    if (bd.buff_values.ContainsKey(Buff.Component_Barrel) && IsBarrel(entity.ShortPrefabName) && RollSuccessful(bd.buff_values[Buff.Component_Barrel]))
                    {
                        var itemDef = GetRandomItemDef(ItemCategory.Component);
                        var quantity = UnityEngine.Random.Range(config.buff_settings.min_components, config.buff_settings.max_components + 1);
                        AddItemsToBarrel(itemDef, quantity, entity);
                    }
                    if (bd.buff_values.ContainsKey(Buff.Electronic_Barrel) && IsBarrel(entity.ShortPrefabName) && RollSuccessful(bd.buff_values[Buff.Electronic_Barrel]))
                    {
                        var itemDef = GetRandomItemDef(ItemCategory.Electrical);
                        var quantity = UnityEngine.Random.Range(config.buff_settings.min_electrical_components, config.buff_settings.max_electrical_components + 1);
                        AddItemsToBarrel(itemDef, quantity, entity);
                    }
                    List<Item> _containerItems = Pool.GetList<Item>();
                    _containerItems.AddRange(entity.inventory.itemList);

                    if (bd.buff_values.ContainsKey(Buff.Scavengers_Ultimate) && IsUltimateEnabled(player, Buff.Scavengers_Ultimate))
                    {
                        foreach (var item in _containerItems)
                        {
                            ScrapItems(item, entity);
                        }
                    }

                    Pool.FreeList(ref _containerItems);                    
                }
            }

            return null;
        }

        [HookMethod("RolledLootPickup")]
        public bool RolledLootPickup(BasePlayer player)
        {
            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd))
            {
                float value;
                if (bd.buff_values.TryGetValue(Buff.Loot_Pickup, out value) && (value >= 1f || RollSuccessful(value))) return true;
            }
            return false;            
        }

        Dictionary<BasePlayer, bool> LastMagnetSuccess = new Dictionary<BasePlayer, bool>();

        void OnBonusItemDropped(Item item, BasePlayer player)
        {
            BuffDetails bd;
            bool result;
            if (LastMagnetSuccess.TryGetValue(player, out result) && result && buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.ContainsKey(Buff.Loot_Pickup))
            {
                GiveItem(player, item);
            }            
            
        }

        bool IsBarrel(string shortname)
        {
            switch (shortname)
            {
                case "loot-barrel-1": return true;
                case "loot-barrel-2": return true;
                case "loot_barrel_1": return true;
                case "loot_barrel_2": return true;
                case "oil_barrel": return true;
                default: return false;
            }
        }

        void HandleLootPickup(BasePlayer player, LootContainer entity)
        {
            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.ContainsKey(Buff.Loot_Pickup) && entity.inventory?.itemList != null && entity.inventory.itemList.Count > 0)
            {
                if (!LastMagnetSuccess.ContainsKey(player)) LastMagnetSuccess.Add(player, false);

                if (RollSuccessful(bd.buff_values[Buff.Loot_Pickup]))
                {
                    if (config.buff_settings.loot_pickup_buff_max_distance > 0 && Vector3.Distance(entity.transform.position, player.transform.position) > config.buff_settings.loot_pickup_buff_max_distance)
                    {
                        LastMagnetSuccess[player] = false;
                        return;
                    }
                    
                    List<Item> item_drops = Pool.GetList<Item>();
                    if (!config.buff_settings.lootPickupBuffMeleeOnly || (player.GetHeldEntity() != null && player.GetHeldEntity() is BaseMelee))
                    {
                        item_drops.AddRange(entity.inventory.itemList);

                        BasePlayer _player = player;
                        NextTick(() =>
                        {
                            if (_player == null) return;
                            foreach (var item in item_drops)
                            {
                                GiveItem(_player, item);
                            }
                            LastMagnetSuccess[player] = false;
                            Pool.FreeList(ref item_drops);
                        });                        
                    }
                    LastMagnetSuccess[player] = true;
                }
                else LastMagnetSuccess[player] = false;
            }
        }

        void OnEntityDeath(BaseEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return;
            var player = info.InitiatorPlayer;
            if (player == null || player.IsNpc || !player.userID.IsSteamId()) return;
            BuffDetails bd;
            switch (entity.ShortPrefabName)
            {
                case "sulfur-ore":
                case "metal-ore":
                case "stone-ore":
                    if (buffDetails.TryGetValue(player.userID, out bd))
                    {
                        var heldEntity = player.GetHeldEntity();
                        if (bd.buff_values.ContainsKey(Buff.Node_Spawn_Chance) && RollSuccessful(bd.buff_values[Buff.Node_Spawn_Chance] * (heldEntity != null && heldEntity is Jackhammer ? config.tools_black_white_list_settings.power_tool_modifier.mining_perk_modifier : 1f)))
                        {
                            Vector3 pos = entity.transform.position;
                            string prefab = entity.PrefabName;
                            Quaternion rot = entity.transform.rotation;
                            
                            NextTick(() =>
                            {                                
                                var newNode = GameManager.server.CreateEntity(prefab, pos, rot);
                                newNode.Spawn();
                                if (player != null && NotificationsOn(player)) PrintToChat(player, lang.GetMessage("NodeSpawned", this, player.UserIDString));
                            });
                        }
                    }
                    break;
                case "loot-barrel-1":
                case "loot-barrel-2":
                case "loot_barrel_1":
                case "loot_barrel_2":
                    AwardXP(player, config.xp_settings.xp_sources.Barrel, entity);
                    HandleLootPickup(player, (LootContainer)entity);
                    break;
                case "roadsign1":
                case "roadsign2":
                case "roadsign3":
                case "roadsign4":
                case "roadsign5":
                case "roadsign6":
                case "roadsign7":
                case "roadsign8":
                case "roadsign9":
                    AwardXP(player, config.xp_settings.xp_sources.RoadSign, entity);
                    HandleLootPickup(player, (LootContainer)entity);
                    break;
                case "oil_barrel":
                    AwardXP(player, config.xp_settings.xp_sources.Barrel, entity);
                    HandleLootPickup(player, (LootContainer)entity);
                    break;
                case "chicken":
                    AwardXP(player, config.xp_settings.xp_sources.SmallAnimal, entity);
                    AddAnimalBuff(player, AnimalBuff.Chicken);
                    break;
                case "boar":
                    AwardXP(player, config.xp_settings.xp_sources.MediumAnimal, entity);
                    AddAnimalBuff(player, AnimalBuff.Boar);
                    break;
                case "stag":
                    AwardXP(player, config.xp_settings.xp_sources.MediumAnimal, entity);
                    AddAnimalBuff(player, AnimalBuff.Stag);
                    break;
                case "wolf":
                    AwardXP(player, config.xp_settings.xp_sources.MediumAnimal, entity);
                    AddAnimalBuff(player, AnimalBuff.Wolf);
                    break;
                case "simpleshark":
                case "bear":
                    AwardXP(player, config.xp_settings.xp_sources.LargeAnimal, entity);
                    AddAnimalBuff(player, AnimalBuff.Bear);
                    break;
                case "polarbear":
                    AwardXP(player, config.xp_settings.xp_sources.LargeAnimal, entity);
                    AddAnimalBuff(player, AnimalBuff.PolarBear);
                    break;
                case "horse":
                    AwardXP(player, config.xp_settings.xp_sources.LargeAnimal, entity);
                    break;
                case "bradleyapc":
                    AwardXP(player, config.xp_settings.xp_sources.BradleyAPC, entity);
                    break;
            }
        }

        void AddItemsToBarrel(ItemDefinition itemDef, int quantity, LootContainer entity)
        {
            entity.inventory.capacity++;
            var item = ItemManager.CreateByName(itemDef.shortname, quantity);
            if (!item.MoveToContainer(entity.inventory)) item.DropAndTossUpwards(entity.transform.position);
        }

        void OnPlayerAddModifiers(BasePlayer player, Item item, ItemModConsumable consumable)
        {
            BuffDetails bd;
            if (!buffDetails.TryGetValue(player.userID, out bd))
            {
                //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {player.displayName}[{player.UserIDString}] - OnPlayerAddModifiers. [Online = {player.IsConnected}]", this, true);
                if (player.IsConnected) PrintToChat(player, lang.GetMessage("FailReload", this, player.UserIDString));
                return;
            }
            if (bd.buff_values.ContainsKey(Buff.Extra_Food_Water))
            {
                var gain = consumable.GetIfType(MetabolismAttribute.Type.Calories);
                if (gain > 0) player.metabolism.calories.value += bd.buff_values[Buff.Extra_Food_Water] * gain;
                gain = consumable.GetIfType(MetabolismAttribute.Type.Hydration);
                if (gain > 0) player.metabolism.hydration.value += bd.buff_values[Buff.Extra_Food_Water] * gain;
            }
            if (bd.buff_values.ContainsKey(Buff.Iron_Stomach))
            {
                if (consumable.GetIfType(MetabolismAttribute.Type.Poison) > 0)
                {
                    player.metabolism.poison.SetValue(player.metabolism.FindAttribute(MetabolismAttribute.Type.Poison).lastValue);
                    if (NotificationsOn(player)) PrintToChat(player, string.Format(lang.GetMessage("IronTummy", this, player.UserIDString), item.info.displayName.english));
                }
            }
        }

        object OnPlayerWound(BasePlayer player, HitInfo info)
        {
            if (player.IsNpc || !player.userID.IsSteamId()) return null;
            BuffDetails bd;
            if (!buffDetails.TryGetValue(player.userID, out bd))
            {
                //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {player.displayName}[{player.UserIDString}] - OnPlayerWound. [Online = {player.IsConnected}]", this, true);
                if (player.IsConnected) PrintToChat(player, lang.GetMessage("FailReload", this, player.UserIDString));
                return null;
            }
            if (bd.buff_values.ContainsKey(Buff.Wounded_Resist) && RollSuccessful(bd.buff_values[Buff.Wounded_Resist]))
            {
                if (NotificationsOn(player)) PrintToChat(player, lang.GetMessage("WoundSave", this, player.UserIDString));
                player.metabolism.radiation_level.SetValue(0);
                player.metabolism.radiation_poison.SetValue(0);
                player.metabolism.oxygen.SetValue(1);
                player.metabolism.temperature.SetValue(15);
                player.metabolism.bleeding.SetValue(0);
                player.health += 10;
                return false;
            }
            return null;
        }

        Dictionary<uint, MiniCopter> tracked_helis = new Dictionary<uint, MiniCopter>();
        Dictionary<uint, MotorRowboat> tracked_rowboats = new Dictionary<uint, MotorRowboat>();
        Dictionary<uint, MotorRowboat> tracked_rhibs = new Dictionary<uint, MotorRowboat>();

        // 444 == vehicle ultimate no damage.
        void AssignSkinToChildren(ulong id, List<BaseEntity> mountables)
        {
            if (mountables != null && mountables.Count > 0)
            {
                List<BaseEntity> tracked_children;
                if (!reduced_damage_entities.TryGetValue(id, out tracked_children)) reduced_damage_entities.Add(id, tracked_children = new List<BaseEntity>());
                foreach (var child in mountables)
                {
                    if (child == null || child.IsDestroyed) continue;
                    tracked_children.Add(child);
                    child.skinID = 444;
                    List<BaseEntity> children = Pool.GetList<BaseEntity>();
                    children.AddRange(child.children.Where(x => x.skinID != 444));
                    var parent = child.GetParentEntity();
                    if (parent != null && parent.skinID != 444) children.Add(parent);
                    AssignSkinToChildren(id, children);
                    Pool.FreeList(ref children);
                }
            }
        }

        void RestoreSkinToChildren(List<BaseEntity> mountables)
        {
            if (mountables == null || mountables.Count == 0) return;
            foreach (var entity in mountables)
            {
                if (entity != null && !entity.IsDestroyed)
                {
                    entity.skinID = 0;
                }
            }
            mountables.Clear();
        }

        object OnEntityTakeDamage(BaseMountable entity, HitInfo info)
        {
            if (entity != null && info != null && info.damageTypes != null && entity.skinID == 444)
            {
                info.damageTypes.ScaleAll(0f);
            }
            return null;
        }

        Dictionary<ulong, List<BaseEntity>> reduced_damage_entities = new Dictionary<ulong, List<BaseEntity>>();

        void OnEntityMounted(BaseMountable entity, BasePlayer player)
        {
            if (player == null || entity == null || player.IsNpc || !player.userID.IsSteamId()) return;

            BuffDetails bd;
            if (!buffDetails.TryGetValue(player.userID, out bd))
            {
                //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {player.displayName}[{player.UserIDString}] - OnEntityMounted. [Online = {player.IsConnected}]", this, true);
                if (player.IsConnected) PrintToChat(player, lang.GetMessage("FailReload", this, player.UserIDString));
                return;
            }

            if (bd.buff_values.ContainsKey(Buff.Vehicle_Ultimate) && IsUltimateEnabled(player, Buff.Vehicle_Ultimate))
            {
                List<BaseEntity> children = Pool.GetList<BaseEntity>();

                if (entity.children != null) children.AddRange(entity.children);
                var parent = entity.GetParentEntity();
                if (parent != null) children.Add(parent);
                AssignSkinToChildren(player.userID, children);
                Pool.FreeList(ref children);
            }

            var vehicle = entity.GetParentEntity();
            if (vehicle == null) return;
            var horse = vehicle as RidableHorse;
            if (horse != null && bd.buff_values.ContainsKey(Buff.Riding_Speed))
            {
                if (Interface.CallHook("STCanModifyHorse", player, horse, bd.buff_values[Buff.Riding_Speed]) != null) return;
                if (Cooking != null && Cooking.IsLoaded && Convert.ToBoolean(Cooking.Call("IsHorseBuffed", horse))) return;
                if (HorseStats.ContainsKey(horse.net.ID)) RestoreHorseStats(horse);
                HorseStats.Add(horse.net.ID, new HorseInfo()
                {
                    current_maxSpeed = horse.maxSpeed,
                    current_runSpeed = horse.runSpeed,
                    current_trotSpeed = horse.trotSpeed,
                    current_turnSpeed = horse.turnSpeed,
                    current_walkSpeed = horse.walkSpeed,
                    player = player,
                    horse = horse
                });
                var modifier = bd.buff_values[Buff.Riding_Speed];
                if (config.buff_settings.horse_buff_info.Increase_Horse_MaxSpeed) horse.maxSpeed += modifier * horse.maxSpeed;
                if (config.buff_settings.horse_buff_info.Increase_Horse_RunSpeed) horse.runSpeed += modifier * horse.runSpeed;
                if (config.buff_settings.horse_buff_info.Increase_Horse_TrotSpeed) horse.trotSpeed += modifier * horse.trotSpeed;
                if (config.buff_settings.horse_buff_info.Increase_Horse_TurnSpeed) horse.turnSpeed += modifier * horse.turnSpeed;
                if (config.buff_settings.horse_buff_info.Increase_Horse_WalkSpeed) horse.walkSpeed += modifier * horse.walkSpeed;
                return;
            }
            else if (vehicle is MiniCopter && bd.buff_values.ContainsKey(Buff.Heli_Fuel_Rate))
            {
                var mini = vehicle as MiniCopter;
                if (mini.fuelPerSec < 0.5f) return;
                var fuelSystem = mini.GetFuelSystem();
                if (fuelSystem == null || fuelSystem.nextFuelCheckTime == float.MaxValue || fuelSystem.GetFuelContainer().HasFlag(BaseEntity.Flags.Locked)) return;
                if (tracked_helis.ContainsKey(mini.net.ID)) return;
                tracked_helis.Add(mini.net.ID, mini);                
                var fuel_rate = default_heli_fuel_rate - (default_heli_fuel_rate * bd.buff_values[Buff.Heli_Fuel_Rate]);
                mini.fuelPerSec = fuel_rate;
            }
            else if (vehicle is MotorRowboat && bd.buff_values.ContainsKey(Buff.Boat_Fuel_Rate))
            {
                var boat = vehicle as MotorRowboat;               

                if (boat is RHIB)
                {
                    if (boat.fuelPerSec < 0.25) return;

                    var fuelSystem = boat.GetFuelSystem();
                    if (fuelSystem == null || fuelSystem.nextFuelCheckTime == float.MaxValue || fuelSystem.GetFuelContainer().HasFlag(BaseEntity.Flags.Locked)) return;

                    if (tracked_rhibs.ContainsKey(boat.net.ID)) return;
                    tracked_rhibs.Add(boat.net.ID, boat);
                    var fuel_rate = default_rhib_fuel_rate - (default_rhib_fuel_rate * bd.buff_values[Buff.Boat_Fuel_Rate]);
                    boat.fuelPerSec = fuel_rate;
                }
                else
                {
                    if (boat.fuelPerSec < 0.1) return;

                    var fuelSystem = boat.GetFuelSystem();
                    if (fuelSystem == null || fuelSystem.nextFuelCheckTime == float.MaxValue || fuelSystem.GetFuelContainer().HasFlag(BaseEntity.Flags.Locked)) return;

                    if (tracked_rowboats.ContainsKey(boat.net.ID)) return;
                    tracked_rowboats.Add(boat.net.ID, boat);
                    var fuel_rate = default_rowboat_fuel_rate - (default_rowboat_fuel_rate * bd.buff_values[Buff.Boat_Fuel_Rate]);
                    boat.fuelPerSec = fuel_rate;
                }
            }
        }

        object OnHammerHit(BasePlayer player, HitInfo info)
        {
            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.ContainsKey(Buff.Vehicle_Mechanic) && info?.HitEntity is BaseVehicle)
            {
                var vehicle = info.HitEntity as BaseVehicle;
                var amount = vehicle.MaxHealth() - vehicle.health;
                if (amount <= 0) return null;
                else
                {
                    vehicle.Heal(amount);
                    if (!string.IsNullOrEmpty(config.effect_settings.repair_effect)) EffectNetwork.Send(new Effect(config.effect_settings.repair_effect, player.transform.position, player.transform.position), player.net.connection);
                    return false;
                }
            }

            return null;
        }

        void RestoreHorseStats(RidableHorse horse)
        {
            HorseInfo hd;
            if (HorseStats.TryGetValue(horse.net.ID, out hd))
            {
                horse.maxSpeed = hd.current_maxSpeed;
                horse.runSpeed = hd.current_runSpeed;
                horse.trotSpeed = hd.current_trotSpeed;
                horse.turnSpeed = hd.current_turnSpeed;
                horse.walkSpeed = hd.current_walkSpeed;
            }
            HorseStats.Remove(horse.net.ID);
        }

        private bool IsZombieNPC(BasePlayer player) => player.GetType().Name.Equals("ZombieNPC");

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (info == null) return;

            if (info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Suicide && config.xp_settings.xp_loss_settings.suicide_death_penalty > 0) LoseXP(player, DeathType.Suicide);

            var attacker = info.Initiator;
            if (attacker == null) return;

            RemoveAnimalBuff(player);
            if (HorseStats.Count > 0)
            {
                List<KeyValuePair<uint, HorseInfo>> temp_horse_list = Pool.GetList<KeyValuePair<uint, HorseInfo>>();
                temp_horse_list.AddRange(HorseStats);
                foreach (KeyValuePair<uint, HorseInfo> kvp in temp_horse_list)
                {
                    if (kvp.Value.player == player)
                    {
                        if (kvp.Value.horse == null)
                        {
                            HorseStats.Remove(kvp.Key);
                            break;
                        }
                        var horse = kvp.Value.horse;
                        if (horse != null && horse.IsAlive())
                        {
                            RestoreHorseStats(horse);
                            break;
                        }
                    }
                }
                Pool.FreeList(ref temp_horse_list);
            }
            if (config.general_settings.drop_bag_on_death && !permission.UserHasPermission(player.UserIDString, "skilltree.bag.keepondeath"))
            {
                PlayerInfo pi;
                if (pcdData.pEntity.TryGetValue(player.userID, out pi) && pi.pouch_items != null && pi.pouch_items.Count > 0 && Interface.CallHook("STOnPouchDrop", player) == null)
                {
                    var bag = GenerateBag(player, 42);
                    if (bag != null && bag.inventory?.itemList != null && bag.inventory.itemList.Count > 0)
                    {
                        var pos = player.transform.position;
                        var rot = player.transform.rotation;
                        timer.Once(0.1f, () =>
                        {
                            bag.inventory.Drop("assets/prefabs/misc/item drop/item_drop.prefab", pos, rot);
                            pi.pouch_items.Clear();
                            containers.Remove(bag.inventory.uid);
                            bag.KillMessage();
                        });
                    }
                }
            }

            if (player == attacker && config.xp_settings.xp_loss_settings.suicide_death_penalty > 0)
            {
                LoseXP(player, DeathType.Suicide);
                return;
            }

            // Attacker is real player
            if (info.InitiatorPlayer != null && !info.InitiatorPlayer.IsNpc && info.InitiatorPlayer.userID.IsSteamId())
            {
                if (IsZombieNPC(player))
                {
                    AwardXP(info.InitiatorPlayer, config.xp_settings.xp_sources.Zombie, player);
                }
                else if (config.xp_settings.xp_loss_settings.pvp_death_penalty > 0) LoseXP(player, DeathType.PVP);
            }
            else if (config.xp_settings.xp_loss_settings.pve_death_penalty > 0 && (attacker.IsNpc || attacker is BaseAnimalNPC))
            {
                LoseXP(player, DeathType.PVE);
            }

            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.ContainsKey(Buff.Medical_Ultimate) && IsUltimateEnabled(player, Buff.Medical_Ultimate))
            {
                SendResurrectionButton(player, player.transform.position);
            }
        }

        void OnPlayerDeath(ScarecrowNPC scarecrow, HitInfo info)
        {
            if (scarecrow == null || info == null || info.InitiatorPlayer == null || info.InitiatorPlayer.IsNpc || !info.InitiatorPlayer.userID.IsSteamId()) return;
            AwardXP(info.InitiatorPlayer, config.xp_settings.xp_sources.Scarecrow, scarecrow);
        }


        void OnPlayerDeath(ScientistNPC npc, HitInfo info)
        {
            if (npc == null || info == null) return;
            var attacker = info.InitiatorPlayer;
            if (attacker == null || attacker.IsNpc) return;
            double betterNPCXP = 0;
            if (npc.skinID == 11162132011012 && config.betternpc_settings.NPC_xp_table.TryGetValue(npc.displayName, out betterNPCXP))
            {
                if (betterNPCXP == 0) betterNPCXP = config.xp_settings.xp_sources.ScientistNormal;
                AwardXP(attacker, betterNPCXP, npc);
            }
            else if (npc.ShortPrefabName == "scientistnpc_heavy") AwardXP(attacker, config.xp_settings.xp_sources.ScientistHeavy, npc);
            else AwardXP(attacker, config.xp_settings.xp_sources.ScientistNormal, npc);                      
            
        }

        void OnPlayerDeath(TunnelDweller npc, HitInfo info)
        {
            if (npc == null || info == null) return;
            var attacker = info.InitiatorPlayer;
            if (attacker == null || attacker.IsNpc || !attacker.userID.IsSteamId()) return;
            AwardXP(attacker, config.xp_settings.xp_sources.TunnelDweller, npc);
        }

        void LoseXP(BasePlayer player, DeathType type)
        {
            if (!player.IsConnected) return;
            if (Interface.CallHook("STOnLoseXP", player) != null || (config.xp_settings.prevent_xp_loss && ((EventManager != null && EventManager.IsLoaded && Convert.ToBoolean(EventManager.Call("IsEventPlayer", player))) || (EventHelper != null && EventHelper.IsLoaded && Convert.ToBoolean(EventHelper.Call("EMPlayerDiedAtEvent", player)))))) return;
            PlayerInfo playerData;
            if (pcdData.pEntity.TryGetValue(player.userID, out playerData))
            {
                var Level = config.level.GetLevel(playerData.xp);
                var LevelStartXP = config.level.GetLevelStartXP(Level) + 1;
                var xp_loss = GetXPLoss(player, playerData.xp - LevelStartXP, type);
                if (playerData.xp - LevelStartXP > xp_loss)
                {
                    playerData.xp -= xp_loss;
                    PrintToChat(player, string.Format(lang.GetMessage("LostXP", this, player.UserIDString), xp_loss));
                }
                else
                {
                    PrintToChat(player, string.Format(lang.GetMessage("LostXP", this, player.UserIDString), playerData.xp - LevelStartXP));
                    playerData.xp = LevelStartXP;
                }
                CheckLevel(player);
                UpdateXP(player, playerData);
            }
        }

        void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            if (player.IsNpc || !player.userID.IsSteamId() || entity == null || entity.GetParentEntity() == null) return;

            if (reduced_damage_entities.ContainsKey(player.userID)) RestoreSkinToChildren(reduced_damage_entities[player.userID]);

            var vehicle = entity.GetParentEntity();
            var horse = vehicle as RidableHorse;
            if (horse != null)
            {
                if (HorseStats.ContainsKey(horse.net.ID))
                {
                    if (horse != null && horse.IsAlive()) RestoreHorseStats(horse);
                    else HorseStats.Remove(horse.net.ID);
                }
            }
            else if (vehicle is BaseBoat)
            {
                BoatInfo bi;
                if (boats.TryGetValue(player.userID, out bi))
                {
                    if (bi.boat != null && bi.boat.IsAlive()) bi.boat.engineThrust = DefaultBoatSpeed(bi.boat.ShortPrefabName);
                    boats.Remove(player.userID);
                }
                BuffDetails bd;
                if (buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.ContainsKey(Buff.Boat_Fuel_Rate))
                {
                    var boat = vehicle as MotorRowboat;
                    if (boat != null)
                    {
                        if (tracked_rowboats.ContainsKey(boat.net.ID) && boat.IsAlive())
                        {
                            boat.fuelPerSec = default_rowboat_fuel_rate;
                            tracked_rowboats.Remove(boat.net.ID);
                        }
                        else if (tracked_rhibs.ContainsKey(boat.net.ID) && boat.IsAlive())
                        {
                            boat.fuelPerSec = default_rhib_fuel_rate;
                            tracked_rowboats.Remove(boat.net.ID);
                        }
                    }
                }
            }
            else if (vehicle is MiniCopter && buffDetails.ContainsKey(player.userID) && buffDetails[player.userID].buff_values.ContainsKey(Buff.Heli_Fuel_Rate))
            {
                var mini = vehicle as MiniCopter;
                if (!tracked_helis.ContainsKey(mini.net.ID)) return;
                mini.fuelPerSec = default_heli_fuel_rate;
                tracked_helis.Remove(mini.net.ID);
            }
        }

        object OnPayForUpgrade(BasePlayer player, BuildingBlock block, ConstructionGrade gradeTarget)
        {
            BuffDetails bd;
            if (player != null && buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.ContainsKey(Buff.Upgrade_Refund) && RollSuccessful(bd.buff_values[Buff.Upgrade_Refund]))
            {
                if (NotificationsOn(player)) PrintToChat(player, lang.GetMessage("FreeUpgrade", this, player.UserIDString));
                return 0;
            }
            return null;
        }

        void ChangeBoatSpeed(BasePlayer player)
        {
            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.ContainsKey(Buff.Boat_Speed))
            {
                var boat = player.GetMountedVehicle() as BaseBoat;
                if (boat == null) return;
                BoatInfo bi;
                if (!boats.TryGetValue(player.userID, out bi))
                {
                    if (boat.engineThrust > DefaultBoatSpeed(boat.ShortPrefabName)) PrintToChat(player, lang.GetMessage("TurboInUse", this, player.UserIDString));
                    else
                    {
                        boats.Add(player.userID, new BoatInfo() { defaultSpeed = boat.engineThrust, boat = boat });
                        boat.engineThrust += bd.buff_values[Buff.Boat_Speed] * DefaultBoatSpeed(boat.ShortPrefabName);
                        PrintToChat(player, lang.GetMessage("TurboToggleOn", this, player.UserIDString));
                    }
                }
                else
                {
                    if (bi.boat == null || bi.boat != boat)
                    {
                        bi.boat.engineThrust = DefaultBoatSpeed(bi.boat.ShortPrefabName);
                        bi.boat = boat;
                        bi.defaultSpeed = DefaultBoatSpeed(boat.ShortPrefabName);
                    }
                    if (boat.engineThrust > DefaultBoatSpeed(boat.ShortPrefabName))
                    {
                        boat.engineThrust = DefaultBoatSpeed(boat.ShortPrefabName);
                        PrintToChat(player, lang.GetMessage("TurboToggleOff", this, player.UserIDString));
                    }
                    else
                    {
                        boat.engineThrust += bd.buff_values[Buff.Boat_Speed] * DefaultBoatSpeed(boat.ShortPrefabName);
                        PrintToChat(player, lang.GetMessage("TurboToggleOn", this, player.UserIDString));
                    }
                }
            }
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input.WasJustPressed(BUTTON.FIRE_THIRD) && player.isMounted)
            {
                ChangeBoatSpeed(player);
            }
        }

        object OnResearchCostDetermine(Item item, ResearchTable researchTable)
        {
            var player = researchTable.user;
            BuffDetails bd;
            if (player != null && buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.ContainsKey(Buff.Research_Refund) && RollSuccessful(bd.buff_values[Buff.Research_Refund]))
            {
                if (NotificationsOn(player)) PrintToChat(player, lang.GetMessage("ScrapRefund", this, player.UserIDString));
                return 0;
            }
            return null;
        }

        void SaveNewNodesToConfig()
        {
            if (QueuedNodes.Count == 0) return;
            int count = 0;
            foreach (var tree in QueuedNodes)
            {
                foreach (var node in tree.Value)
                {
                    if (!config.trees.ContainsKey(tree.Key)) continue;
                    if (!config.trees[tree.Key].nodes.ContainsKey(node.Key))
                    {
                        config.trees[tree.Key].nodes.Add(node.Key, node.Value);
                        count++;
                    }
                }
            }
            Puts($"Saved {count} new nodes. Reloading plugin.");
            SaveConfig();
            NewNodesAdded = false;
            QueuedNodes.Clear();

            Interface.Oxide.ReloadPlugin(Name);
        }

        void OnServerSave()
        {
            SaveData();
            if (NewNodesAdded)
            {
                SaveNewNodesToConfig();
                foreach (var player in BasePlayer.activePlayerList)
                {
                    DoClear(player);
                    LoggingOff(player);
                    HandleNewConnection(player);
                }
            }
        }

        void HandleNewConnection(BasePlayer player)
        {
            SetupPlayer(player.userID, player.displayName);
            UpdateInstancedData(player);
            PlayerInfo playerData = pcdData.pEntity[player.userID];
            if (playerData.xp_hud) UpdateXP(player, playerData);
            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.ContainsKey(Buff.Metabolism_Boost)) IncreaseCalories(player, bd.buff_values[Buff.Metabolism_Boost]);
            LoggedOn(player, playerData);
        }

        List<LootContainer> looted_containers = new List<LootContainer>();
        List<ItemDefinition> component_item_list = new List<ItemDefinition>();
        List<ItemDefinition> electrical_item_list = new List<ItemDefinition>();

        ItemDefinition GetRandomItemDef(ItemCategory category)
        {
            if (category == ItemCategory.Component) return component_item_list.GetRandom();
            if (category == ItemCategory.Electrical) return electrical_item_list.GetRandom();
            return null;
        }

        void AddItemToContainer(LootContainer container, ItemDefinition itemDef, int quantity)
        {
            if (container == null || container.inventory == null || itemDef == null) return;
            container.inventory.capacity++;
            container.inventorySlots++;
            ItemManager.CreateByName(itemDef.shortname, quantity)?.MoveToContainer(container.inventory);
        }

        void OnEntityKill(LootContainer container)
        {
            if (container != null)
            {
                looted_containers.Remove(container);
                looted_crates.Remove(container);
            }
        }

        void CanLootEntity(BasePlayer player, LootContainer container)
        {
            if (container == null || player == null) return;

            BuffDetails bd;
            if (player != null && buffDetails.TryGetValue(player.userID, out bd))
            {
                if (!looted_containers.Contains(container))
                {
                    if (Interface.CallHook("STCanReceiveBonusLootFromContainer", player, container) != null)
                    {
                        looted_containers.Add(container);
                        return;
                    }

                    if (config.loot_settings.loot_crate_whitelist != null && config.loot_settings.loot_crate_whitelist.Count > 0 && !config.loot_settings.loot_crate_whitelist.Contains(container.PrefabName)) return;
                    if (bd.buff_values.ContainsKey(Buff.Component_Chest) && RollSuccessful(bd.buff_values[Buff.Component_Chest]) && container.inventorySlots < 12)
                    {
                        var itemDef = GetRandomItemDef(ItemCategory.Component);
                        var quantity = UnityEngine.Random.Range(config.buff_settings.min_components, config.buff_settings.max_components);
                        AddItemToContainer(container, itemDef, quantity);
                    }
                    if (bd.buff_values.ContainsKey(Buff.Electronic_Chest) && RollSuccessful(bd.buff_values[Buff.Electronic_Chest]) && container.inventorySlots < 12)
                    {
                        var itemDef = GetRandomItemDef(ItemCategory.Electrical);
                        var quantity = UnityEngine.Random.Range(config.buff_settings.min_electrical_components, config.buff_settings.max_electrical_components);
                        AddItemToContainer(container, itemDef, quantity);
                    }
                    if (bd.buff_values.ContainsKey(Buff.Extra_Scrap_Crate) && RollSuccessful(bd.buff_values[Buff.Extra_Scrap_Crate]))
                    {
                        var itemDef = ItemManager.FindItemDefinition("scrap");
                        var quantity = UnityEngine.Random.Range(config.buff_settings.min_extra_scrap, config.buff_settings.max_extra_scrap);
                        AddItemToContainer(container, itemDef, quantity);
                    }
                    if (bd.buff_values.ContainsKey(Buff.DeepSeaLooter) && DeepSeaLooterLootTable.ContainsKey(container.PrefabName) && RollSuccessful(bd.buff_values[Buff.DeepSeaLooter]))
                    {
                        var loot = DeepSeaLooterLootTable[container.PrefabName].GetRandom();
                        container.inventory.capacity++;
                        container.inventorySlots++;
                        ItemManager.CreateByName(loot.shortname, UnityEngine.Random.Range(loot.min, loot.max)).MoveToContainer(container.inventory);
                    }
                    looted_containers.Add(container);
                }
            }
        }

        [PluginReference]
        private Plugin ImageLibrary, Economics, ServerRewards, EventManager, BotReSpawn, Cooking, UINotify, ZombieHorde, EventHelper;

        Dictionary<Buff, BuffType> BuffBuffType = new Dictionary<Buff, BuffType>();

        private Dictionary<string, string> loadOrder = new Dictionary<string, string>();

        private KeyValuePair<string, string> ExtraPocketsImg;
        private KeyValuePair<string, ulong> ExtraPocketsImgSkin;

        void OnServerInitialized(bool initial)
        {
            var foundNewContent = false;
            if (config.general_settings.image_cache_source.Equals("url", StringComparison.OrdinalIgnoreCase) && ImageLibrary == null)
            {
                Puts("Setting cache type to skin as ImageLibrary is not loaded.");
                config.general_settings.image_cache_source = "skinid";
                foundNewContent = true;
            }
            bool allfalse = true;
            foreach (var tree in config.trees)
            {
                if (tree.Value.enabled) allfalse = false;
                var defaultTree = DefaultTrees.ContainsKey(tree.Key) ? DefaultTrees[tree.Key] : null;
                if (defaultTree == null) continue;
                foreach (var node in tree.Value.nodes)
                {
                    var defaultNode = defaultTree != null && defaultTree.nodes.ContainsKey(node.Key) ? defaultTree.nodes[node.Key] : null;
                    if (defaultNode == null) continue;
                    foreach (var link in OldLinks)
                    {
                        if (link.Equals(node.Value.icon_url, StringComparison.OrdinalIgnoreCase))
                        {
                            node.Value.icon_url = defaultNode.icon_url;                            
                            Puts($"Replacing the URL for {node.Key} with {node.Value.icon_url}");
                            foundNewContent = true;
                        }
                    }
                }
            }

            if (allfalse)
            {
                foreach (var tree in config.trees)
                    tree.Value.enabled = true;
                foundNewContent = true;
            }
            
            if (pcdData.highest_player == 0) bonus_given = true;

            if (config.general_settings.respec_cost_override.Count == 0)
            {
                config.general_settings.respec_cost_override.Add("vip", Math.Round(config.general_settings.respec_cost / 2, 0));
                foundNewContent = true;
            }

            if (config.general_settings.max_skill_points_override.Count == 0)
            {
                config.general_settings.max_skill_points_override.Add("vip", config.general_settings.max_skill_points + (Convert.ToInt32(config.general_settings.max_skill_points * 0.2)));
                foundNewContent = true;
            }

            if (config.xp_settings.xp_loss_settings.xp_loss_override.Count == 0)
            {
                config.xp_settings.xp_loss_settings.xp_loss_override.Add("vip", 0.5);
                foundNewContent = true;
            }

            if (config.rested_xp_settings.rested_xp_modifier_perm_mod.Count == 0)
            {
                config.rested_xp_settings.rested_xp_modifier_perm_mod.Add("restedxp.10", 0.1f);
                foundNewContent = true;
            }

            foreach (var perm in config.xp_settings.xp_loss_settings.xp_loss_override)
            {
                if (!permission.PermissionExists("skilltree." + perm.Key, this)) permission.RegisterPermission("skilltree." + perm.Key, this);
            }
            config.level.CalculateTable(config.general_settings.max_player_level);
            if (Configuration.ExperienceInfo.UpdatedTable) foundNewContent = true;
            if (BotReSpawn != null)
            {
                var BotReSpawnBots = (Dictionary<string, List<ulong>>)BotReSpawn?.Call("BotReSpawnBots");
                foreach (var profile in BotReSpawnBots)
                {
                    if (!config.misc_settings.botrespawn_profiles.ContainsKey(profile.Key))
                    {
                        config.misc_settings.botrespawn_profiles.Add(profile.Key, config.xp_settings.xp_sources.default_botrespawn);
                        Puts($"Added new BotReSpawn profile: {profile.Key}. Allocated default xp value of: {config.xp_settings.xp_sources.default_botrespawn}.");
                        foundNewContent = true;
                    }
                }
            }
            // Checks for new trees added to the plugin between updates and adds them to the users config.

            Dictionary<string, Configuration.TreeInfo> trees = DefaultTrees;
            foreach (var tree in DefaultTrees)
            {
                if (!config.trees.ContainsKey(tree.Key) && config.wipe_update_settings.auto_update_trees)
                {
                    config.trees.Add(tree.Key, tree.Value);
                    Puts($"Adding new tree: {tree.Key.ToString()}");
                    foundNewContent = true;
                }
                else
                {
                    foreach (var node in tree.Value.nodes)
                    {
                        if (!config.trees[tree.Key].nodes.ContainsKey(node.Key) && config.wipe_update_settings.auto_update_nodes)
                        {
                            Puts($"Adding new node: {node.Key}");
                            config.trees[tree.Key].nodes.Add(node.Key, node.Value);
                            foundNewContent = true;
                        }
                        var configNodes = config.trees[tree.Key].nodes;
                        if (config.general_settings.image_cache_source.Equals("skinid", StringComparison.OrdinalIgnoreCase) && configNodes.ContainsKey(node.Key) && configNodes[node.Key].skin < 1)
                        {
                            configNodes[node.Key].skin = node.Value.skin;
                            foundNewContent = true;
                        }
                    }
                }
            }
            // Gets and stores all bp defs. Also stores category info.
            foreach (var itemDef in ItemManager.GetItemDefinitions())
            {
                ItemDefs.Add(itemDef.shortname, itemDef);
                if (itemDef.Blueprint != null && itemDef.Blueprint.userCraftable)
                {
                    if (!item_BPs.ContainsKey(itemDef.shortname)) item_BPs.Add(itemDef.shortname, itemDef.Blueprint);
                }

                if (itemDef.category == ItemCategory.Electrical && !config.tools_black_white_list_settings.comp_blacklist.Contains(itemDef.shortname)) electrical_item_list.Add(itemDef);
                else if (itemDef.category == ItemCategory.Component && !config.tools_black_white_list_settings.comp_blacklist.Contains(itemDef.shortname)) component_item_list.Add(itemDef);
            }
            Puts($"Blueprint count: {item_BPs.Count}");

            if (config.ultimate_settings.ultimate_skinning.enabled_buffs.Count == 0)
            {
                config.ultimate_settings.ultimate_skinning.enabled_buffs = DefaultAnimalBuffs;
                foundNewContent = true;
            }
            DeepSeaLooterLootTable = GetUnderwaterLoot();
            SharkLootTable = GetSharkLoot();

            if (config.loot_settings.mining_loot_table.Count == 0)
            {
                config.loot_settings.mining_loot_table = DefaultLootItems;
                foundNewContent = true;
            }

            if (config.loot_settings.wc_loot_table.Count == 0)
            {
                config.loot_settings.wc_loot_table = DefaultLootItems;
                foundNewContent = true;
            }

            if (config.loot_settings.skinning_loot_table.Count == 0)
            {
                config.loot_settings.skinning_loot_table = DefaultLootItems;
                foundNewContent = true;
            }

            if (config.loot_settings.fishing_loot_table.Count == 0)
            {
                config.loot_settings.fishing_loot_table = DefaultLootItems;
                foundNewContent = true;
            }

            if (config.ultimate_settings.ultimate_mining.trigger_on_item_change && config.ultimate_settings.ultimate_mining.tools_list.Count == 0)
            {
                config.ultimate_settings.ultimate_mining.tools_list = DefaultUltimateToolsList;
                foundNewContent = true;
            }

            if (foundNewContent) SaveConfig();

            // Chat command stuff
            foreach (var chatcommand in config.chat_commands.score_chat_cmd)
            {
                cmd.AddChatCommand(chatcommand, this, "CheckScoreBoard");
                cmd.AddConsoleCommand(chatcommand, this, "CheckScoreBoardConsole");
            }
            foreach (var chatcommand in config.chat_commands.chat_cmd)
            {
                cmd.AddChatCommand(chatcommand, this, "SendMenuCMD");
            }
            // Image lib stuff

            foreach (var tree in config.trees)
            {
                foreach (var node in tree.Value.nodes)
                {
                    if (!node.Value.enabled) continue;

                    if (config.general_settings.image_cache_source.Equals("url", StringComparison.OrdinalIgnoreCase) || node.Value.skin == 0)
                    {
                        if (string.IsNullOrEmpty(ExtraPocketsImg.Key) && node.Value.buff_info.Key == Buff.ExtraPockets)
                        {
                            ExtraPocketsImg = new KeyValuePair<string, string>("ExtraPocketsButton", node.Value.icon_url);
                            loadOrder.Add("ExtraPocketsButton", node.Value.icon_url);
                        }
                        loadOrder.Add(node.Key, node.Value.icon_url);
                        
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(ExtraPocketsImgSkin.Key) && node.Value.buff_info.Key == Buff.ExtraPockets)
                        {
                            ExtraPocketsImgSkin = new KeyValuePair<string, ulong>("ExtraPocketsButton", node.Value.skin);
                        }
                    }
                    if (!NodeSkinDirectory.ContainsKey(node.Key)) NodeSkinDirectory.Add(node.Key, node.Value.skin);
                    if (!BuffBuffType.ContainsKey(node.Value.buff_info.Key)) BuffBuffType.Add(node.Value.buff_info.Key, node.Value.buff_info.Value);                   
                }
            }
            // Updates the players level based on the config.
            foreach (var kvp in pcdData.pEntity)
            {
                kvp.Value.current_level = config.level.GetLevel(kvp.Value.xp);
                if (kvp.Value.achieved_level == 0 && kvp.Value.current_level != 0) kvp.Value.achieved_level = kvp.Value.current_level;

                if (kvp.Value.pouch_items?.Count > 0)
                {
                    List<ItemInfo> items = Pool.GetList<ItemInfo>();
                    items.AddRange(kvp.Value.pouch_items);
                    foreach (var item in items)
                    {
                        if (item.amount <= 0)
                        {
                            Puts($"Found an item with 0 quantity in {kvp.Value.name ?? kvp.Key.ToString()}'s pouch - Removing.");
                            kvp.Value.pouch_items.Remove(item);
                        }
                    }
                }
            }

            // If players are online when this is run, we set up their data.
            if (BasePlayer.activePlayerList.Count > 0)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    HandleNewConnection(player);                    
                }
            }
            if (boats.Count > 0)
            {
                foreach (var kvp in boats)
                {
                    if (kvp.Value.boat != null) kvp.Value.boat.engineThrust = DefaultBoatSpeed(kvp.Value.boat.ShortPrefabName);
                }
            }
            cmd.AddChatCommand(config.chat_commands.turbo_cmd, this, "ChangeBoatSpeed");
            if (!config.chat_commands.use_input_key_boat)
            {
                Unsubscribe("OnPlayerInput");
            }
            if (config.xp_settings.xp_sources.Mission == 0) Unsubscribe("OnMissionSucceeded");
            if (config.xp_settings.xp_sources.LootBradleyCrate == 0 && config.xp_settings.xp_sources.LootHackedCrate == 0 && config.xp_settings.xp_sources.LootHeliCrate == 0) Unsubscribe("OnLootEntity");
            if (config.xp_settings.xp_sources.Win_HungerGames == 0) Unsubscribe("HGWinner");
            if (config.xp_settings.xp_sources.Win_ScubaArena == 0) Unsubscribe("SAWinner");
            if (config.xp_settings.xp_sources.Win_Skirmish == 0)
            {
                Unsubscribe("SAWinner");
                Unsubscribe("SAWinners");
            }
            if (config.general_settings.image_cache_source.Equals("url", StringComparison.OrdinalIgnoreCase))
            {
                loadOrder.Add("arrow_down_double", "https://www.dropbox.com/s/a1aysr6qmcuinyb/arrow_down_double.png?dl=1");
                loadOrder.Add("arrow_left_double", "https://www.dropbox.com/s/tx5vgr3m9bujvde/arrow_left_double.png?dl=1");
                loadOrder.Add("arrow_right_double", "https://www.dropbox.com/s/6ns3a41qwdn74h8/arrow_right_double.png?dl=1");
                loadOrder.Add("arrow_up_double", "https://www.dropbox.com/s/yqygkxfsyput635/arrow_up_double.png?dl=1");
                loadOrder.Add("arrow_down_single", "https://www.dropbox.com/s/jqi9ulzgj8pq024/arrow_down_single.png?dl=1");
                loadOrder.Add("arrow_left_single", "https://www.dropbox.com/s/ht2pol52oc4q5k9/arrow_left_single.png?dl=1");
                loadOrder.Add("arrow_right_single", "https://www.dropbox.com/s/aixjnroopq9vess/arrow_right_single.png?dl=1");
                loadOrder.Add("arrow_up_single", "https://www.dropbox.com/s/ud9fnx07bv724v2/arrow_up_single.png?dl=1");                
            }
            if (loadOrder.Count > 0)
            {
                Puts($"Loading {loadOrder.Count} images into ImageLibrary");
                ImageLibrary?.Call("ImportImageList", this.Name, loadOrder, 0ul, config.general_settings.replace_on_reload, new Action(SkillTreeImagesReady));
            }
            else
            {
                NextTick(() => SkillTreeImagesReady());
            }
            LoadBuffs();

            if (pcdData.pEntity != null)
            {
                foreach (var kvp in pcdData.pEntity)
                {
                    var point_tally = kvp.Value.available_points;
                    if (kvp.Value.buff_values != null)
                    {
                        foreach (var point in kvp.Value.buff_values)
                        {
                            point_tally += point.Value;
                        }
                    }
                    var points_should_have = config.general_settings.points_per_level * kvp.Value.achieved_level;
                    if (point_tally < points_should_have)
                    {
                        kvp.Value.available_points += points_should_have - point_tally;
                        Puts($"{kvp.Key} had less points than they should. Added {points_should_have - point_tally} points to their pool.");
                    }
                }
            }
            UpdateScoreBoard();

            cmd.AddChatCommand(config.ultimate_settings.ultimate_mining.find_node_cmd, this, "TriggerMiningUltimateFromCMD");
            cmd.AddChatCommand(config.ultimate_settings.ultimate_harvesting.gene_chat_command, this, "SetPlantGenes");

            if (!string.IsNullOrEmpty(config.xp_settings.xp_display_col_modified)) ModifiedCol = config.xp_settings.xp_display_col_modified;
            else ModifiedCol = "00b6ff";

            if (!string.IsNullOrEmpty(config.xp_settings.xp_display_col_unmodified)) UnmodifiedCol = config.xp_settings.xp_display_col_unmodified;
            else UnmodifiedCol = "ffffff";

            CheckHarvestingBlacklist = config.buff_settings.harvest_yield_blacklist.Count > 0;

            if (!config.ultimate_settings.ultimate_mining.trigger_on_item_change) Unsubscribe(nameof(OnActiveItemChanged));
        }

        Dictionary<string, ulong> ArrowSkins = new Dictionary<string, ulong>()
        {
            ["arrow_down_double"] = 2873060319,
            ["arrow_left_double"] = 2873060538,
            ["arrow_right_double"] = 2873060617,
            ["arrow_up_double"] = 2873060659,
            ["arrow_down_single"] = 2873060760,
            ["arrow_left_single"] = 2873060807,
            ["arrow_right_single"] = 2873060847,
            ["arrow_up_single"] = 2873060907
        };
        

        private bool ImagesLoaded;

        private void SkillTreeImagesReady()
        {
            loadOrder.Clear();
            loadOrder = null;
            ImagesLoaded = true;
            Puts($"Loaded all images for SkillTree.");
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (buffDetails.ContainsKey(player.userID) && buffDetails[player.userID].buff_values.ContainsKey(Buff.ExtraPockets) && pcdData.pEntity.ContainsKey(player.userID) && pcdData.pEntity[player.userID].extra_pockets_button) SendExtraPocketsButton(player);
            }
        }

        void OnPlayerConnected(BasePlayer player) => HandleNewConnection(player);

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            DoClear(player);
            LoggingOff(player);
        }

        void DoClear(BasePlayer player)
        {
            UpdatePlayerData(player.userID);
            TreeData.Remove(player.userID);
            //if (buffDetails.ContainsKey(player.userID)) buffDetails[player.userID].buff_values.Clear();
            buffDetails.Remove(player.userID);
            notifiedPlayers.Remove(player.userID);
            player.metabolism.calories.max = 500f;
            player.metabolism.hydration.max = 250f;
            player.SendNetworkUpdate();
            RemoveFromAllBuffs(player.userID);
            RemoveAnimalBuff(player);
            DestroyRegen(player);
            DestroyWaterBreathing(player);
            DestroyInstantUntie(player);
            RemovePerms(player.UserIDString);
        }

        // UserIDString, Node name, List of perms.
        void RemovePerms(string id)
        {
            Dictionary<string, Dictionary<string, string>> perms;
            if (Tracked_perms.TryGetValue(id, out perms))
            {
                foreach (var node in perms.Values)
                {
                    foreach (var perm in node.Keys)
                    {
                        permission.RevokeUserPermission(id, perm.Trim());
                    }
                }
                Tracked_perms.Remove(id);
            }
        }

        #endregion

        #region Experience

        [ChatCommand("updatexptable")]
        void UpdateXPTable(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, "skilltree.admin")) return;
            if (config.level == null) config.level = new Configuration.ExperienceInfo();
            config.level.CalculateTable(config.general_settings.max_player_level > 0 ? config.general_settings.max_player_level : 100);
            SaveConfig();
            Puts("Updated xp table.");
        }

        int CheckLevel(BasePlayer player)
        {
            PlayerInfo playerData;
            if (!pcdData.pEntity.TryGetValue(player.userID, out playerData)) return 0;
            var level = config.level.GetLevel(playerData.xp);
            // If we are max level, we exit the method with the max level.
            if (config.general_settings.max_player_level > 0 && playerData.current_level >= config.general_settings.max_player_level) return config.general_settings.max_player_level;
            //We run the following block of code if our current level is less than the calculated level of our xp.
            if (playerData.current_level < level)
            {
                var max_skill_points = GetMaxSkillPoints(player);
                // If the configured max level is 0 OR our new level is less than/equal to the max level, we run the following code block.
                if (config.general_settings.max_player_level == 0 || level <= config.general_settings.max_player_level)
                {
                    // We check to see if the highest level achieved by the player is less than the new level.
                    PrintToChat(player, string.Format(lang.GetMessage("LevelEarn", this, player.UserIDString), playerData.achieved_level < level ? (config.general_settings.points_per_level * (level - playerData.current_level)) : 0, level));
                    if (playerData.achieved_level < level)
                    {
                        // After confirming the achieved level is < level, we check to see if the player has hit the maximum number of skill points, or if config max skill points is 0, then award them with skill points.
                        if (max_skill_points == 0 || (playerData.current_level * config.general_settings.points_per_level < max_skill_points)) playerData.available_points += config.general_settings.points_per_level * (level - playerData.current_level);
                        // We set this current level as the maximum level achieved. This is to prevent skill points being awarded if they have reached this level before and somehow lost xp/level.
                        playerData.achieved_level = level;
                    }
                    // Increases the current level of the player to the new level.
                    Interface.CallHook("STOnPlayerLevel", player, playerData.current_level, level);
                    GiveLevelRewards(player, level, level - playerData.current_level);
                    playerData.current_level = level;
                    if (config.notification_settings.notifySettings.level_up_notification.Key != null)
                    {
                        var str = string.Format(lang.GetMessage(config.notification_settings.notifySettings.level_up_notification.Key, this, player.UserIDString), level, playerData.available_points);
                        SendNotify(player, str, config.notification_settings.notifySettings.level_up_notification.Value);
                    }
                }
                // Otherwise we assume we are hitting max level.
                else
                {
                    // We check the amount of levels the player gained from max level.
                    var levels_gained = config.general_settings.max_player_level - playerData.current_level;
                    PrintToChat(player, string.Format(lang.GetMessage("LevelEarn", this, player.UserIDString), config.general_settings.points_per_level * levels_gained, config.general_settings.max_player_level));
                    // We check to see if the highest level achieved by the player is less than max level.
                    if (playerData.achieved_level < config.general_settings.max_player_level)
                    {
                        // After confirming the achieved level is < max level, we check to see if the player has hit the maximum number of skill points, or if config max skill points is 0, then award them with skill points.
                        if (max_skill_points == 0 || (playerData.current_level * config.general_settings.points_per_level < max_skill_points)) playerData.available_points += config.general_settings.points_per_level * levels_gained;
                        // We set this current level as the maximum level achieved. This is to prevent skill points being awarded if they have reached this level before and somehow lost xp/level.
                        playerData.achieved_level = config.general_settings.max_player_level;
                        GiveLevelRewards(player, config.general_settings.max_player_level, levels_gained);
                        if (config.notification_settings.notifySettings.level_up_notification.Key != null)
                        {
                            var str = string.Format(lang.GetMessage(config.notification_settings.notifySettings.level_up_notification.Key, this, player.UserIDString), config.general_settings.max_player_level, playerData.available_points);
                            SendNotify(player, str, config.notification_settings.notifySettings.level_up_notification.Value);
                        }
                    }
                    //We set the players level to max level.
                    playerData.current_level = config.general_settings.max_player_level;
                }
                // Sends a network effect ot the player only if configured to.
                if (!string.IsNullOrEmpty(config.effect_settings.level_effect)) EffectNetwork.Send(new Effect(config.effect_settings.level_effect, player.transform.position, player.transform.position), player.net.connection);
            }
            // We assume the players current level is higher than it should be and adjust it back to the new level. This could be due to an xp change.
            else if (playerData.current_level != level && (config.general_settings.max_player_level == 0 || config.general_settings.max_player_level <= level)) playerData.current_level = level;
            return level;
        }
        
        void SendNotify(BasePlayer player, string message, int type)
        {
            if (UINotify == null || !UINotify.IsLoaded || string.IsNullOrEmpty(message)) return;
            UINotify.Call("SendNotify", player.userID, type, message);
        }

        void GiveRewards(BasePlayer player, int level)
        {
            LevelReward _rewards;
            if (config.general_settings.level_rewards.TryGetValue(level, out _rewards))
            {
                foreach (var reward in _rewards.reward_commands)
                {
                    string[] command_string = reward.Key.Split(' ');
                    if (command_string != null && command_string.Length > 0)
                    {
                        if (command_string.Length == 1)
                        {
                            try
                            {
                                Server.Command(reward.Key);
                            }
                            catch
                            {
                                //LogToFile("CommandFailureLog", $"[{DateTime.Now}] Failed to run command for {player.displayName}[{player.userID}] - Command: {reward.Key}", this, true);
                            }
                        }
                            
                        else
                        {
                            string command = command_string[0];
                            List<string> args = Pool.GetList<string>();
                            foreach (var arg in command_string.Skip(1))
                            {
                                if (arg.Contains("{id}")) args.Add(arg.Replace("{id}", player.UserIDString));
                                else if (arg.Contains("{name}")) args.Add(arg.Replace("{name}", player.displayName));
                                else args.Add(arg);
                            }
                            if (command.Equals("say", StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    Server.Command(command, string.Join(" ", args));
                                }
                                catch
                                {
                                    //LogToFile("CommandFailureLog", $"[{DateTime.Now}] Failed to run command for {player.displayName}[{player.userID}] - Command: {command} {string.Join(" ", args)}", this, true);
                                }
                            }
                            else
                            {
                                try
                                {
                                    Server.Command(command, args);
                                }
                                catch
                                {
                                    //LogToFile("CommandFailureLog", $"[{DateTime.Now}] Failed to run command for {player.displayName}[{player.userID}] - Command: {command} {string.Join(" ", args)}", this, true);
                                }
                            }
                                
                            if (!string.IsNullOrEmpty(reward.Value)) PrintToChat(player, reward.Value);
                            Pool.FreeList(ref args);
                        }
                    }
                }
            }
        }

        void RunResetCommands(string playerID, int level_achieved)
        {
            foreach (var level in config.general_settings.level_rewards)
            {
                if (level.Key <= level_achieved)
                {
                    foreach (var _command in level.Value.reset_commands)
                    {
                        var command_string = _command.Split(' ');
                        if (command_string != null && command_string.Length > 0)
                        {
                            string command = command_string[0];
                            List<string> args = Pool.GetList<string>();
                            foreach (var arg in command_string.Skip(1))
                            {
                                if (arg.Contains("{id}")) args.Add(arg.Replace("{id}", playerID));
                                else if (arg.Contains("{name}"))
                                {
                                    string name = null;                                        
                                    foreach (var player in BasePlayer.allPlayerList)
                                    {
                                        if (player.UserIDString == playerID)
                                        {
                                            name = player.displayName;
                                            break;
                                        }
                                    }
                                    args.Add(arg.Replace("{name}", name ?? playerID));
                                }
                                else args.Add(arg);
                            }

                            if (args == null || args.Count < 1)
                            {
                                try
                                {
                                    Server.Command(command);
                                }
                                catch
                                {
                                    //LogToFile("CommandFailureLog", $"[{DateTime.Now}] Failed to run command for {player.displayName}[{player.userID}] - Command: {command}", this, true);
                                }
                            }
                                
                            else if (command.Equals("say", StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    Server.Command(command, string.Join(" ", args));
                                }
                                catch
                                {
                                    //LogToFile("CommandFailureLog", $"[{DateTime.Now}] Failed to run command for {player.displayName}[{player.userID}] - Command: {command} args: {string.Join(" ", args)}", this, true);
                                }
                            }
                                
                            else
                            {
                                try
                                {
                                    Server.Command(command, args);
                                }
                                catch
                                {
                                    //LogToFile("CommandFailureLog", $"[{DateTime.Now}] Failed to run command for {player.displayName}[{player.userID}] - Command: {command} args: {string.Join(" ", args)}", this, true);
                                }
                            }                                

                            Pool.FreeList(ref args);
                        }
                    }
                }
            }
        }

        [ConsoleCommand("stgiveitem")]
        void STGiveItem(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, perm_admin)) return;
            //stgiveitem <id> <shortname> <quantity> <skinID> <displayName>
            if (arg.Args == null || arg.Args.Length < 4)
            {
                arg.ReplyWith(lang.GetMessage("stgiveitemUsage", this, player?.UserIDString ?? null));
                return;
            }
            if (!arg.Args[0].IsSteamId())
            {
                arg.ReplyWith(string.Format(lang.GetMessage("stgiveitemInvalidID", this, player?.UserIDString ?? null), arg.Args[0]));
                return;
            }
            var target = FindPlayerByID(arg.Args[0], player ?? null);
            if (target == null)
            {
                arg.ReplyWith(string.Format(lang.GetMessage("stgiveitemNoPlayerFound", this, player?.UserIDString ?? null), arg.Args[0]));
                return;
            }
            var def = ItemManager.FindItemDefinition(arg.Args[1]);
            if (def == null || string.IsNullOrEmpty(def.shortname))
            {
                arg.ReplyWith(string.Format(lang.GetMessage("stgiveitemInvalidShortname", this, player?.UserIDString ?? null), arg.Args[1]));
                return;
            }

            if (!arg.Args[2].IsNumeric())
            {
                arg.ReplyWith(string.Format(lang.GetMessage("stgiveitemQuantityInvalid", this, player?.UserIDString ?? null), arg.Args[2]));
                return;
            }
            var quantity = Convert.ToInt32(arg.Args[2]);
            quantity = Math.Max(quantity, 1);

            if (!arg.Args[3].IsNumeric())
            {
                arg.ReplyWith(string.Format(lang.GetMessage("stgiveitemSkinInvalid", this, player?.UserIDString ?? null), arg.Args[3]));
                return;
            }

            var skinID = Convert.ToUInt64(arg.Args[3]);            
            
            string displayName = null;
            if (arg.Args.Length > 4) displayName = string.Join(" ", arg.Args.Skip(4));
            var item = ItemManager.CreateByName(arg.Args[1], quantity, skinID);
            if (!string.IsNullOrEmpty(displayName)) item.name = displayName;
            target.GiveItem(item);
            arg.ReplyWith($"Gave {item.amount}x {item.name ?? item.info.displayName.english} to {target.displayName}");
        }

        void GiveLevelRewards(BasePlayer player, int newLevel, int levelsGained = 1)
        {
            if (levelsGained == 0) return;
            else if (levelsGained == 1) GiveRewards(player, newLevel);
            else
            {
                int startLevel = newLevel - levelsGained;
                for (int i = startLevel + 1; i < newLevel + 1; i++)
                {
                    GiveRewards(player, i);
                }
            }

            //if (config.general_settings.level_reward.amount > 0)
            //{
            //    switch (config.general_settings.level_reward.rewardType)
            //    {
            //        case "scrap":
            //            GiveItem(player, ItemManager.CreateByName("scrap", config.general_settings.level_reward.amount * levelsGained));
            //            //player.GiveItem(ItemManager.CreateByName("scrap", config.level_reward.amount * levelsGained));
            //            break;
            //        case "economics":
            //            Economics?.Call("Deposit", player.UserIDString, Convert.ToDouble(config.general_settings.level_reward.amount * levelsGained));
            //            break;
            //        case "srp":
            //            ServerRewards?.Call("AddPoints", player.userID, config.general_settings.level_reward.amount * levelsGained);
            //            break;
            //        case "custom":
            //            if (config.general_settings.respec_currency_custom == null || string.IsNullOrEmpty(config.general_settings.respec_currency_custom.shortname)) break;
            //            var item = ItemManager.CreateByName(config.general_settings.respec_currency_custom.shortname, config.general_settings.level_reward.amount * levelsGained, config.general_settings.respec_currency_custom.skin);
            //            if (!string.IsNullOrEmpty(config.general_settings.respec_currency_custom.displayName)) item.name = config.general_settings.respec_currency_custom.displayName;
            //            //player.GiveItem(item);
            //            GiveItem(player, item);
            //            break;
            //        case "command":
            //            if (config.)
            //    }
            //    PrintToChat(player, string.Format(lang.GetMessage("LevelReward", this, player.UserIDString), config.general_settings.level_reward.amount * levelsGained, config.general_settings.level_reward.rewardType, newLevel));
            //}
        }

        #endregion

        #region Helpers      

        private static List<T> FindResourceEntitiesOfType<T>(Vector3 pos, float radius, int m = -1) where T : EntityComponent<BaseEntity>
        {
            int hits = Physics.OverlapSphereNonAlloc(pos, radius, Vis.colBuffer, m, QueryTriggerInteraction.Collide);
            List<T> entities = new List<T>();
            for (int i = 0; i < hits; i++)
            {
                var entity = Vis.colBuffer[i]?.ToBaseEntity();
                if (entity is T) entities.Add(entity as T);
                Vis.colBuffer[i] = null;
            }
            return entities;
        }

        private static List<T> FindEntitiesOfType<T>(Vector3 pos, float radius, int m = -1) where T : BaseEntity
        {
            int hits = Physics.OverlapSphereNonAlloc(pos, radius, Vis.colBuffer, m, QueryTriggerInteraction.Collide);
            List<T> entities = new List<T>();
            for (int i = 0; i < hits; i++)
            {
                var entity = Vis.colBuffer[i]?.ToBaseEntity();
                if (entity is T) entities.Add(entity as T);
                Vis.colBuffer[i] = null;
            }
            return entities;
        }

        void GiveItem(BasePlayer player, Item item)
        {
            if (player == null)
            {
                item.Remove();
                return;
            }
            if (player.inventory.containerMain.itemList != null)
            {
                var moved = false;
                int amount = item.amount;
                foreach (var _item in player.inventory.containerMain.itemList)
                {
                    if (_item.info.stackable < 2) continue;                    
                    if (_item.skin == item.skin && _item.info.shortname == item.info.shortname)
                    {
                        if ((item.name != null || _item.name != null) && item.name != _item.name) continue;
                        if (item.MoveToContainer(player.inventory.containerMain, _item.position)) moved = true;
                    }
                    if (moved)
                    {
                        player.Command("note.inv", new object[] { item.info.itemid, amount, item.name != null ? item.name : String.Empty, (int)BaseEntity.GiveItemReason.PickedUp });
                        return;
                    }

                }

                foreach (var _item in player.inventory.containerBelt.itemList)
                {
                    if (_item.info.stackable < 2) continue;
                    if (_item.skin == item.skin && _item.info.shortname == item.info.shortname)
                    {
                        if ((item.name != null || _item.name != null) && item.name != _item.name) continue;
                        if (item.MoveToContainer(player.inventory.containerBelt, _item.position)) moved = true;
                    }
                    if (moved)
                    {
                        player.Command("note.inv", new object[] { item.info.itemid, amount, item.name != null ? item.name : String.Empty, (int)BaseEntity.GiveItemReason.PickedUp });
                        return;
                    }

                }

                player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
            }

        }

        void LoggingOff(BasePlayer player, PlayerInfo pi = null)
        {
            if (pi == null && !pcdData.pEntity.TryGetValue(player.userID, out pi)) return;
            pi.logged_off = DateTime.Now;
        }

        void LoggedOn(BasePlayer player, PlayerInfo pi)
        {
            if (pi == null && !pcdData.pEntity.TryGetValue(player.userID, out pi))
            {
                SetupPlayer(player.userID, player.displayName);
                pi = pcdData.pEntity[player.userID];
            }
            if (pi.logged_off == null)
            {
                pi.logged_off = DateTime.Now;
                return;
            }
            if (config.rested_xp_settings.rested_xp_enabled)
            {
                var offlineHours = Convert.ToInt32((DateTime.Now - pi.logged_off).TotalHours);
                if (offlineHours > 0)
                {
                    pi.xp_bonus_pool += GetModifiedRestedXP(player, offlineHours * config.rested_xp_settings.rested_xp_per_hour);
                    if (config.rested_xp_settings.rested_xp_pool_max > 0 && pi.xp_bonus_pool > config.rested_xp_settings.rested_xp_pool_max) pi.xp_bonus_pool = config.rested_xp_settings.rested_xp_pool_max;
                    PrintToChat(player, string.Format(lang.GetMessage("RestedNotification", this, player.UserIDString), config.rested_xp_settings.rested_xp_rate * 100, pi.xp_bonus_pool));
                }
            }
            pi.logged_off = DateTime.Now;
        }

        double GetModifiedRestedXP(BasePlayer player, double rested_xp)
        {
            var mod = 0f;
            foreach (var perm in config.rested_xp_settings.rested_xp_modifier_perm_mod)
            {
                if (perm.Value > mod && permission.UserHasPermission(player.UserIDString, perm.Key))
                    mod = perm.Value;
            }

            return rested_xp + (rested_xp * mod);
        }

        [HookMethod("GiveSkillPoints")]
        public void GiveSkillPoints(BasePlayer player, int amount)
        {
            PlayerInfo playerData;
            if (!pcdData.pEntity.TryGetValue(player.userID, out playerData))
            {
                //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {player.displayName}[{player.UserIDString}] - GiveSkillPoints. [Online = {player.IsConnected}]", this, true);
                if (player.IsConnected) PrintToChat(player, lang.GetMessage("FailReload", this, player.UserIDString));
                return;
            }
            playerData.available_points += amount;
            PrintToChat(player, string.Format(lang.GetMessage("ReceivedSP", this, player.UserIDString), amount, playerData.available_points));
        }

        float DefaultBoatSpeed(string shortname)
        {
            if (shortname == "rhib") return 1500f;
            else return 600f;
        }

        BasePlayer FindPlayerByID(string id, BasePlayer searchingPlayer = null)
        {
            if (!id.IsSteamId()) return null;
            var player = BasePlayer.activePlayerList.Where(x => x.UserIDString == id).FirstOrDefault();
            if (player == null)
            {
                if (searchingPlayer != null) PrintToChat(searchingPlayer, $"No player found matching ID: {id}");
                else Puts($"No player found matching ID: {id}");
            }
            return player ?? null;
        }

        private BasePlayer FindPlayerByName(string Playername, BasePlayer SearchingPlayer = null)
        {
            var lowered = Playername.ToLower();
            var targetList = BasePlayer.allPlayerList.Where(x => x.displayName.ToLower().Contains(lowered)).OrderBy(x => x.displayName.Length);
            if (targetList.Count() == 1)
            {
                return targetList.First();
            }
            if (targetList.Count() > 1)
            {
                if (targetList.First().displayName.Equals(Playername, StringComparison.OrdinalIgnoreCase))
                {
                    return targetList.First();
                }
                if (SearchingPlayer != null)
                {
                    PrintToChat(SearchingPlayer, string.Format(lang.GetMessage("MorePlayersFound", this, SearchingPlayer.UserIDString), String.Join(",", targetList.Select(x => x.displayName))));
                }
                else Puts(string.Format(lang.GetMessage("MorePlayersFound", this), String.Join(",", targetList.Select(x => x.displayName))));
                return null;
            }
            if (targetList.Count() == 0)
            {
                if (SearchingPlayer != null)
                {
                    PrintToChat(SearchingPlayer, string.Format(lang.GetMessage("NoMatch", this, SearchingPlayer.UserIDString), Playername));
                }
                else Puts(string.Format(lang.GetMessage("NoMatch", this), Playername));
                return null;
            }
            return null;
        }

        bool RollSuccessful(float luck)
        {
            var roll = UnityEngine.Random.Range(0f, 100f);
            return (roll >= 100f - (luck * 100));
        }

        double GetXPModifier(string id, PlayerInfo pi, out bool modified, bool noMod = false)
        {            
            modified = false;
            if (noMod) return 1;
            bool hasModifier = false;
            double result = 0;
            // Checks each permission that you have created in the config for xp override.
            foreach (var perm in config.xp_settings.xp_perm_modifier)
            {
                // If the permissions value is greater than the value stored in result, result is set to the new value.
                if (permission.UserHasPermission(id, "skilltree." + perm.Key) && perm.Value > result)
                {
                    result = perm.Value;
                    hasModifier = true;
                }                    
            }
            // If we don't have any special perms, we set the mod to default (1.0)
            if (!hasModifier) result = 1;
            if (config.rested_xp_settings.rested_xp_enabled && pi.xp_bonus_pool > 0)
            {
                // If rested XP is enabled, then we add the rested xp value on top of our result.
                result += config.rested_xp_settings.rested_xp_rate;
                modified = true;
            }
            if (TOD_Sky.Instance.IsNight && config.xp_settings.night_settings.night_xp_gain_modifier != 1)
            {
                // If night time xp gains are enabled, we add (or remove) that value onto our result as well.
                result += config.xp_settings.night_settings.night_xp_gain_modifier - 1;
                modified = true;
            }
            
            return result;
        }

        bool IsGodMode(BasePlayer player)
        {
            return player.IsGod();
        }

        [HookMethod("AwardXP")]
        public void AwardXP(BasePlayer player, double value, BaseEntity source = null, bool noMod = false)
        {
            if (player == null || player.IsNpc || !player.userID.IsSteamId() || value == 0) return;
            if (!permission.UserHasPermission(player.UserIDString, "skilltree.xp") || value == 0 || Interface.CallHook("STCanGainXP", player, source ?? null) != null) return;
            if (!config.xp_settings.allow_godemode_xp && IsGodMode(player)) return;
            PlayerInfo playerData;
            if (!pcdData.pEntity.TryGetValue(player.userID, out playerData))
            {
                //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {player.displayName}[{player.UserIDString}] - AwardXP. [Online = {player.IsConnected}]", this, true);
                if (player.IsConnected) PrintToChat(player, lang.GetMessage("FailReload", this, player.UserIDString));
                return;
            }
            bool modified;
            var xp = (value * GetXPModifier(player.UserIDString, playerData, out modified, noMod));
            playerData.xp += xp;
            if (playerData.xp_bonus_pool > 0) playerData.xp_bonus_pool -= xp;
            if (playerData.xp_drops)
            {
                DisplayXPMenu(player, xp, modified);
            }
            CheckLevel(player);
            if (playerData.xp_hud) UpdateXP(player, playerData);
            if (permission.UserHasPermission(player.UserIDString, "skilltree.chat") && !notifiedPlayers.Contains(player.userID))
            {
                if (config.chat_commands.chat_cmd.Count > 1) PrintToChat(player, string.Format(lang.GetMessage("AccessReminder", this, player.UserIDString), config.chat_commands.chat_cmd.First()));
                notifiedPlayers.Add(player.userID);
            }
        }

        void IncreaseCalories(BasePlayer player, float modifier)
        {
            player.metabolism.calories.max = 500 + (500 * modifier);
            player.metabolism.hydration.max = 250 + (250 * modifier);
            player.SendNetworkUpdate();
        }

        void LevelUpNode(BasePlayer player, string tree, string name)
        {
            TreeInfo ti;
            if (!TreeData.TryGetValue(player.userID, out ti))
            {
                //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {player.displayName}[{player.UserIDString}] - LevelUpNode. [Online = {player.IsConnected}]", this, true);
                if (player.IsConnected) PrintToChat(player, lang.GetMessage("FailReload", this, player.UserIDString));
                return;
            }
            var max_skill_points = GetMaxSkillPoints(player);
            if (max_skill_points > 0 && ti.total_points_spent >= max_skill_points)
            {
                PrintToChat(player, lang.GetMessage("MaxSP", this, player.UserIDString));
                return;
            }
            NodesInfo nsi = ti.trees[tree];
            NodeInfo ni;
            if (ti.trees[tree].nodes.TryGetValue(name, out ni))
            {
                if (ni.level_current >= ni.level_max)
                {
                    PrintToChat(player, lang.GetMessage("MaxedNode", this, player.UserIDString));
                    return;
                }
                if ((ni.tier == 2 && nsi.points_spent < config.general_settings.t2_points_required) || (ni.tier == 3 && nsi.points_spent < config.general_settings.t3_points_required) || (ni.tier == 4 && nsi.points_spent < config.general_settings.ultimate_points_required))
                {
                    PrintToChat(player, string.Format(lang.GetMessage("NoPrevTierIncUltimate", this, player.UserIDString), config.general_settings.t2_points_required, config.general_settings.t3_points_required, config.general_settings.ultimate_points_required));
                    return;
                }
                PlayerInfo playerData;
                if (!pcdData.pEntity.TryGetValue(player.userID, out playerData))
                {
                    SetupPlayer(player.userID);
                    playerData = pcdData.pEntity[player.userID];
                }

                if (playerData.available_points == 0)
                {
                    PrintToChat(player, lang.GetMessage("MaxedSkillPoints", this, player.UserIDString));
                    return;
                }
                if (max_skill_points > 0 && nsi.points_spent >= max_skill_points)
                {
                    PrintToChat(player, lang.GetMessage("AssignedMaxedSkillPoints", this, player.UserIDString));
                    return;
                }
                if (ni.level_current == 0)
                {
                    PrintToChat(player, string.Format(lang.GetMessage("UnlockedFirstNode", this, player.UserIDString), name, ni.level_max));
                    if (!string.IsNullOrEmpty(config.effect_settings.skill_point_unlock_effect)) EffectNetwork.Send(new Effect(config.effect_settings.skill_point_unlock_effect, player.transform.position, player.transform.position), player.net.connection);
                    if (ni.buffInfo.Key == Buff.ExtraPockets && playerData.extra_pockets_button) SendExtraPocketsButton(player);                    
                }
                else
                {
                    PrintToChat(player, string.Format(lang.GetMessage("UnlockedNode", this, player.UserIDString), name, ni.level_current + 1, ni.level_max));
                    if (!string.IsNullOrEmpty(config.effect_settings.skill_point_level_effect)) EffectNetwork.Send(new Effect(config.effect_settings.skill_point_level_effect, player.transform.position, player.transform.position), player.net.connection);
                }

                ni.level_current++;                
                playerData.available_points--;
                nsi.points_spent++;
                ti.total_points_spent++;
                if (!playerData.buff_values.ContainsKey(name)) playerData.buff_values.Add(name, ni.level_current);
                else playerData.buff_values[name] = ni.level_current;
                BuffDetails bd;
                if (!buffDetails.TryGetValue(player.userID, out bd)) buffDetails.Add(player.userID, bd = new BuffDetails());
                if (!bd.buff_values.ContainsKey(ni.buffInfo.Key)) bd.buff_values.Add(ni.buffInfo.Key, ni.level_current * ni.value_per_buff);
                else bd.buff_values[ni.buffInfo.Key] += ni.value_per_buff;
                if (ni.buffInfo.Key == Buff.Metabolism_Boost) IncreaseCalories(player, bd.buff_values[ni.buffInfo.Key]);
                else if (ni.buffInfo.Key == Buff.HealthRegen) UpdateRegen(player, bd.buff_values[ni.buffInfo.Key]);
                else if (ni.buffInfo.Key == Buff.WaterBreathing) UpdateWaterBreathing(player, bd.buff_values[ni.buffInfo.Key]);
                else if (ni.buffInfo.Key == Buff.InstantUntie) UpdateInstantUntie(player);
                AddBuffs(player.userID, ni.buffInfo.Key);

                switch (ni.buffInfo.Key)
                {
                    case Buff.Build_Craft_Ultimate:
                    case Buff.Combat_Ultimate:
                    case Buff.Harvester_Ultimate:
                    case Buff.Medical_Ultimate:
                    case Buff.Mining_Ultimate:
                    case Buff.Scavengers_Ultimate:
                    case Buff.Skinning_Ultimate:
                    case Buff.Vehicle_Ultimate:
                    case Buff.Woodcutting_Ultimate:
                        HandleUltimateToggle(player, ni.buffInfo.Key, playerData);
                        break;
                    case Buff.ExtraPockets:
                        SendExtraPocketsButton(player);
                        break;
                }

                HandlePerms(player, tree, name, ni.level_current);
            }
        }

        void HandlePerms(BasePlayer player, string tree, string node, int level) => HandlePerms(player.UserIDString, tree, node, level);

        // UserIDString, Node name, List of perms.
        Dictionary<string, Dictionary<string, Dictionary<string, string>>> Tracked_perms = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();

        void HandlePerms(string id, string tree, string node, int level)
        {
            Configuration.TreeInfo ti;
            Configuration.TreeInfo.NodeInfo ni;
            if (config.trees.TryGetValue(tree, out ti) && ti.nodes.TryGetValue(node, out ni) && ni.permissions != null)
            {
                if (ni.permissions.perms.Count == 0) return;
                if (level > 0 && !ni.permissions.perms.ContainsKey(level)) return;                
                if (!Tracked_perms.ContainsKey(id)) Tracked_perms.Add(id, new Dictionary<string, Dictionary<string, string>>());
                Dictionary<string, string> tracked_perms;
                if (!Tracked_perms[id].TryGetValue(node, out tracked_perms)) Tracked_perms[id].Add(node, tracked_perms = new Dictionary<string, string>());

                foreach (var perm in ni.permissions.perms)
                {
                    foreach (var str in perm.Value.perms_list)
                    {
                        //Puts($"Revoked permission {str} from {id}.");
                        permission.RevokeUserPermission(id, str.Key.Trim());
                        tracked_perms.Remove(str.Key);
                    }
                }

                if (level > 0)
                {
                    foreach (var perm in ni.permissions.perms[level].perms_list)
                    {
                        //Puts($"Granted permission {perm} to {id}.");
                        permission.GrantUserPermission(id, perm.Key.Trim(), null);
                        tracked_perms.Add(perm.Key, perm.Value);
                    }
                }
            }
        }

        Buff GetBuffType(string name)
        {
            if (BuffTypes.ContainsKey(name)) return BuffTypes[name];
            else return 0;
        }

        float GetBuffModifier(ulong id, Buff buff)
        {
            if (buff == Buff.None) return 0;
            BuffDetails bd;
            if (!buffDetails.TryGetValue(id, out bd) || !bd.buff_values.ContainsKey(buff)) return 0;
            return bd.buff_values[buff];
        }

        float GetBuffModifier(ulong id, string name)
        {
            var buff = GetBuffType(name);
            if (buff == Buff.None) return 0;
            BuffDetails bd;
            if (!buffDetails.TryGetValue(id, out bd) || !bd.buff_values.ContainsKey(buff)) return 0;
            return bd.buff_values[buff];
        }

        int GetMaxSkillPoints(BasePlayer player)
        {
            var highest = config.general_settings.max_skill_points;
            if (highest == 0) return 0;
            foreach (var perm in config.general_settings.max_skill_points_override)
            {
                if (!permission.UserHasPermission(player.UserIDString, "skilltree." + perm.Key)) continue;
                if (perm.Value == 0) return 0;
                if (perm.Value > highest) highest = perm.Value;
            }
            return highest;
        }

        double GetRespecCost(BasePlayer player)
        {
            var lowest = config.general_settings.respec_cost;
            if (lowest == 0) return 0;
            foreach (var perm in config.general_settings.respec_cost_override)
            {
                if (!permission.UserHasPermission(player.UserIDString, "skilltree." + perm.Key)) continue;
                if (perm.Value == 0) return 0;
                if (perm.Value < lowest) lowest = perm.Value;
            }
            if (config.general_settings.respec_multiplier > 0)
            {
                PlayerInfo pi;
                if (!pcdData.pEntity.TryGetValue(player.userID, out pi)) return lowest;
                lowest += Convert.ToDouble(lowest * pi.respec_multiplier);
            }
            return lowest;
        }

        double GetXPLoss(BasePlayer player, double currentXP, DeathType type)
        {
            // currentXP is the xp into the current level.
            var lowest = ((type == DeathType.PVE ? config.xp_settings.xp_loss_settings.pve_death_penalty : type == DeathType.PVP ? config.xp_settings.xp_loss_settings.pvp_death_penalty : config.xp_settings.xp_loss_settings.suicide_death_penalty) / 100) * currentXP;
            if (lowest == 0) return 0;
            foreach (var perm in config.xp_settings.xp_loss_settings.xp_loss_override)
            {
                if (!permission.UserHasPermission(player.UserIDString, "skilltree." + perm.Key)) continue;
                if (perm.Value == 0) return 0;
                if ((perm.Value / 100) * currentXP < lowest) lowest = perm.Value;
            }
            return lowest;
        }

        void UpdateInstancedData(BasePlayer player)
        {
            TreeInfo ti;
            if (!TreeData.TryGetValue(player.userID, out ti) && !player.IsNpc && player.userID.IsSteamId()) TreeData.Add(player.userID, ti = new TreeInfo());
            if (ti == null) return;
            foreach (var cfg in config.trees)
            {
                if (!cfg.Value.enabled || (config.general_settings.require_tree_perms && !permission.UserHasPermission(player.UserIDString, "skilltree." + cfg.Key.ToString()) && !permission.UserHasPermission(player.UserIDString, "skilltree.all"))) continue;
                NodesInfo ni;
                if (!ti.trees.TryGetValue(cfg.Key, out ni)) ti.trees.Add(cfg.Key, ni = new NodesInfo());
                foreach (var node in cfg.Value.nodes)
                {
                    if (!node.Value.enabled) continue;
                    NodeInfo nodeData;
                    if (!ni.nodes.TryGetValue(node.Key, out nodeData)) ni.nodes.Add(node.Key, nodeData = new NodeInfo());
                    nodeData.buffInfo = node.Value.buff_info;
                    nodeData.level_max = node.Value.max_level;
                    nodeData.tier = node.Value.tier;
                    nodeData.value_per_buff = node.Value.value_per_buff;
                    if (nodeData.tier == 4)
                    {
                        if (nodeData.buffInfo.Key == Buff.Build_Craft_Ultimate)
                        {
                            string ult_str = config.ultimate_settings.ultimate_buildCraft.success_chance < 100 ? string.Format(lang.GetMessage("Build_Craft_Ultimate_Description_Addition", this, player.UserIDString), config.ultimate_settings.ultimate_buildCraft.success_chance) : "";
                            nodeData.description = string.Format(lang.GetMessage(nodeData.buffInfo.Key.ToString(), this, player.UserIDString), ult_str);
                        }
                        else if (nodeData.buffInfo.Key == Buff.Harvester_Ultimate)
                        {
                            string ult_str = config.ultimate_settings.ultimate_harvesting.cooldown > 0 ? string.Format(lang.GetMessage("Harvesting_Ultimate_Description_Addition", this, player.UserIDString), config.ultimate_settings.ultimate_harvesting.cooldown) : "";
                            nodeData.description = string.Format(lang.GetMessage(nodeData.buffInfo.Key.ToString(), this, player.UserIDString), ult_str);
                        }
                        else if (nodeData.buffInfo.Key == Buff.Mining_Ultimate)
                        {
                            nodeData.description = string.Format(lang.GetMessage(nodeData.buffInfo.Key.ToString(), this, player.UserIDString), Math.Round(config.ultimate_settings.ultimate_mining.distance_from_player, 0));
                        }
                        else if (nodeData.buffInfo.Key == Buff.Woodcutting_Ultimate)
                        {
                            nodeData.description = string.Format(lang.GetMessage(nodeData.buffInfo.Key.ToString(), this, player.UserIDString), Math.Round(config.ultimate_settings.ultimate_woodcutting.distance_from_player, 0));
                        }
                        else if (nodeData.buffInfo.Key == Buff.Skinning_Ultimate)
                        {
                            string ult_str = config.ultimate_settings.ultimate_skinning.enabled_buffs.FirstOrDefault(x => x.Value > 0).Value > 0 ? string.Format(string.Join("</color>, <color=#4214388>", config.ultimate_settings.ultimate_skinning.enabled_buffs.Where(x => x.Value > 0).Select(x => x.Key.ToString()))) : "";
                            nodeData.description = string.Format(lang.GetMessage(nodeData.buffInfo.Key.ToString(), this, player.UserIDString), ult_str);
                        }
                        else if (nodeData.buffInfo.Key == Buff.Medical_Ultimate)
                        {
                            nodeData.description = string.Format(lang.GetMessage(nodeData.buffInfo.Key.ToString(), this, player.UserIDString), Math.Round(config.ultimate_settings.ultimate_medical.resurrection_chance, 0));
                        }
                        else if (nodeData.buffInfo.Key == Buff.Combat_Ultimate)
                        {
                            string formattedString = "";
                            var active = 0;
                            if (config.ultimate_settings.ultimate_combat.scientists_enabled) active++;
                            if (config.ultimate_settings.ultimate_combat.players_enabled) active++;
                            if (config.ultimate_settings.ultimate_combat.scientists_enabled) active++;

                            if (config.ultimate_settings.ultimate_combat.scientists_enabled)
                            {
                                formattedString += "<color=#4214388>Scientists</color>";
                                if (active == 2) formattedString += " and ";
                                if (active == 3) formattedString += ", ";
                            }
                            if (config.ultimate_settings.ultimate_combat.animals_enabled)
                            {
                                formattedString += "<color=#4214388>Animals</color>";
                                if (active == 3) formattedString += " and ";
                            }
                            if (config.ultimate_settings.ultimate_combat.players_enabled) formattedString += "<color=#4214388>Players</color>";

                            nodeData.description = string.Format(lang.GetMessage(nodeData.buffInfo.Key.ToString(), this, player.UserIDString), config.ultimate_settings.ultimate_combat.health_scale * 100, formattedString);
                        }
                        else if (nodeData.buffInfo.Key == Buff.Scavengers_Ultimate)
                        {
                            nodeData.description = lang.GetMessage(nodeData.buffInfo.Key.ToString(), this, player.UserIDString);
                        }
                        else if (nodeData.buffInfo.Key == Buff.Vehicle_Ultimate)
                        {
                            nodeData.description = string.Format(lang.GetMessage(nodeData.buffInfo.Key.ToString(), this, player.UserIDString), config.ultimate_settings.ultimate_vehicle.reduce_by * 100);
                        }
                        else
                        {
                            if (nodeData.buffInfo.Value == BuffType.Percentage) nodeData.description = string.Format(lang.GetMessage(nodeData.buffInfo.Key.ToString(), this, player.UserIDString), nodeData.value_per_buff / 1 * 100);
                            else if (nodeData.buffInfo.Value == BuffType.IO) nodeData.description = lang.GetMessage(nodeData.buffInfo.Key.ToString(), this, player.UserIDString);
                            else nodeData.description = string.Format(lang.GetMessage(nodeData.buffInfo.Key.ToString(), this, player.UserIDString), nodeData.value_per_buff);
                        }
                    }
                    else if (nodeData.buffInfo.Value == BuffType.Percentage) nodeData.description = string.Format(lang.GetMessage(nodeData.buffInfo.Key.ToString(), this, player.UserIDString), nodeData.value_per_buff / 1 * 100);
                    else if (nodeData.buffInfo.Value == BuffType.IO) nodeData.description = lang.GetMessage(nodeData.buffInfo.Key.ToString(), this, player.UserIDString);
                    else nodeData.description = string.Format(lang.GetMessage(nodeData.buffInfo.Key.ToString(), this, player.UserIDString), nodeData.value_per_buff);
                    PlayerInfo playerData;
                    if (!pcdData.pEntity.TryGetValue(player.userID, out playerData) || !playerData.buff_values.ContainsKey(node.Key)) nodeData.level_current = 0;
                    else nodeData.level_current = playerData.buff_values[node.Key];
                    ni.points_spent += nodeData.level_current;
                    ti.total_points_spent += nodeData.level_current;

                    BuffDetails bd;
                    if (!buffDetails.TryGetValue(player.userID, out bd)) buffDetails.Add(player.userID, bd = new BuffDetails());
                    if (nodeData.level_current > 0)
                    {
                        if (!bd.buff_values.ContainsKey(nodeData.buffInfo.Key)) bd.buff_values.Add(nodeData.buffInfo.Key, nodeData.level_current * nodeData.value_per_buff);
                        else bd.buff_values[nodeData.buffInfo.Key] += nodeData.level_current * nodeData.value_per_buff;
                        AddBuffs(player.userID, nodeData.buffInfo.Key);
                        switch (nodeData.buffInfo.Key)
                        {
                            case Buff.Build_Craft_Ultimate:
                            case Buff.Combat_Ultimate:
                            case Buff.Harvester_Ultimate:
                            case Buff.Medical_Ultimate:
                            case Buff.Mining_Ultimate:
                            case Buff.Scavengers_Ultimate:
                            case Buff.Skinning_Ultimate:
                            case Buff.Vehicle_Ultimate:
                            case Buff.Woodcutting_Ultimate:
                                HandleUltimateToggle(player, nodeData.buffInfo.Key, playerData);
                                break;
                            case Buff.ExtraPockets:
                                SendExtraPocketsButton(player);
                                break;
                        }
                        HandlePerms(player, cfg.Key, node.Key, nodeData.level_current);
                    }


                    if (!BuffTypes.ContainsKey(node.Key)) BuffTypes.Add(node.Key, nodeData.buffInfo.Key);
                }
                if (buffDetails[player.userID].buff_values.ContainsKey(Buff.Metabolism_Boost)) IncreaseCalories(player, buffDetails[player.userID].buff_values[Buff.Metabolism_Boost]);
                if (buffDetails[player.userID].buff_values.ContainsKey(Buff.HealthRegen)) UpdateRegen(player, buffDetails[player.userID].buff_values[Buff.HealthRegen]);
                if (buffDetails[player.userID].buff_values.ContainsKey(Buff.WaterBreathing)) UpdateWaterBreathing(player, buffDetails[player.userID].buff_values[Buff.WaterBreathing]);
                if (buffDetails[player.userID].buff_values.ContainsKey(Buff.InstantUntie)) UpdateInstantUntie(player);
            }
        }

        bool bonus_given;
        void SetupPlayer(ulong id, string name = null)
        {
            if (!id.IsSteamId()) return;
            if (!pcdData.pEntity.ContainsKey(id)) pcdData.pEntity.Add(id, new PlayerInfo() { xp_hud_pos = config.general_settings.pump_bar_settings.offset_default, xp_drops = config.xp_settings.enable_xp_drop_by_default, logged_off = DateTime.Now, available_points = config.wipe_update_settings.starting_skill_points });
            if (!bonus_given && id == pcdData.highest_player)
            {
                pcdData.pEntity[id].available_points += config.wipe_update_settings.bonus_skill_points_amount;
                pcdData.highest_player = 0;
            }
            if (name != null) pcdData.pEntity[id].name = name;
        }

        // Only called when a player's data is being removed.
        void UpdatePlayerData(ulong id)
        {
            SetupPlayer(id);
            var playerData = pcdData.pEntity[id];
            TreeInfo pi;
            if (TreeData.TryGetValue(id, out pi))
            {
                foreach (var tree in pi.trees)
                {
                    foreach (var node in tree.Value.nodes)
                    {
                        HandlePerms(id.ToString(), tree.Key, node.Key, 0);
                        if (!playerData.buff_values.ContainsKey(node.Key)) continue;
                        else if (node.Value.level_current > 0) playerData.buff_values[node.Key] = node.Value.level_current;
                    }
                }
            }
        }

        void RespecPlayer(BasePlayer player)
        {
            player.metabolism.calories.max = 500f;
            player.metabolism.hydration.max = 250f;
            player.SendNetworkUpdate();
            DestroyRegen(player);
            DestroyWaterBreathing(player);
            DestroyInstantUntie(player);
            TreeInfo ti;
            if (!TreeData.TryGetValue(player.userID, out ti)) return;
            //TreeData, BuffDetails, PlayerData
            ti.total_points_spent = 0;
            foreach (var tree in ti.trees)
            {
                tree.Value.points_spent = 0;
                foreach (var node in tree.Value.nodes)
                {
                    HandlePerms(player, tree.Key, node.Key, 0);
                    node.Value.level_current = 0;
                }                    
            }
            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd))
            {
                bd.buff_values.Clear();
            }
            PlayerInfo playerData;
            if (pcdData.pEntity.TryGetValue(player.userID, out playerData))
            {
                var pointsBack = 0;
                if (playerData.buff_values.Count > 0)
                {
                    foreach (var buff in playerData.buff_values)
                        pointsBack += buff.Value;
                    playerData.available_points += pointsBack;
                }
                if (playerData.available_points < playerData.current_level * config.general_settings.points_per_level) playerData.available_points = playerData.current_level * config.general_settings.points_per_level;
                playerData.buff_values.Clear();
                if (playerData.pouch_items != null & playerData.pouch_items.Count > 0)
                {
                    var bag = GenerateBag(player, playerData.pouch_items.Count + 1);
                    if (bag.inventory?.itemList != null)
                    {
                        List<Item> giveItems = Pool.GetList<Item>();
                        giveItems.AddRange(bag.inventory.itemList);

                        foreach (var item in giveItems)
                        {
                            GiveItem(player, item);
                            //player.GiveItem(item);
                        }
                        playerData.pouch_items.Clear();
                        Pool.FreeList(ref giveItems);
                        PrintToChat(player, "Your items were removed from your pouch and returned to you.");
                    }
                }
                playerData.ultimate_settings.Clear();
                CuiHelper.DestroyUi(player, "ExtraPocketsButton");
                RemoveFromAllBuffs(player.userID);                
            }
        }

        #endregion

        #region Menu

        private void SkillTreeBackPanel(BasePlayer player)
        {
            if (!ImagesLoaded) PrintToChat(player, "Still caching images...");
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0.0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-0.287 0.343", OffsetMax = "0.312 -0.337" }
            }, "ContentUI", "SkillTreeBackPanel");

            CuiHelper.DestroyUi(player, "SkillTreeBackPanel");
            CuiHelper.AddUi(player, container);
        }

        Dictionary<string, ulong> NodeSkinDirectory = new Dictionary<string, ulong>();

        void SendSkillTreeMenu(BasePlayer player, string tree = null, string selected_node = null)
        {
            if (!permission.UserHasPermission(player.UserIDString, "skilltree.tree"))
            {
                PrintToChat(player, lang.GetMessage("NoPermsTree", this, player.UserIDString));
                CuiHelper.DestroyUi(player, "SkillTreeBackPanel");
                return;
            }
            TreeInfo ti;
            if (!TreeData.TryGetValue(player.userID, out ti))
            {
                //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {player.displayName}[{player.UserIDString}] - SendSkillTreeMenu. [Online = {player.IsConnected}]", this, true);
                if (player.IsConnected) PrintToChat(player, lang.GetMessage("FailReload", this, player.UserIDString));
                return;
            }
            if (ti.trees == null || ti.trees.Count == 0)
            {
                CuiHelper.DestroyUi(player, "SkillTreeBackPanel");
                PrintToChat(player, "You do not have any tree permissions. Please apply permission skilltree.<category> if you want to allocate individual trees, or skilltree.all if you want players to access all categories.");
                return;
            }
            if (tree == null) tree = ti.trees.First().Key;
            var ni = ti.trees[tree];
            NodeInfo foundNode;

            if (selected_node != null)
            {
                foundNode = ni.nodes.Where(x => x.Key == selected_node).Select(x => x.Value).First();
            }
            else foundNode = null;
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-0.351 -0.332", OffsetMax = "0.349 0.338" }
            }, "ContentUI", "SkillTree");
            container.Add(new CuiElement
            {
                Name = "SkillTree_Title",
                Parent = "SkillTree",
                Components = {
                    new CuiTextComponent { Text = $"{lang.GetMessage(tree, this, player.UserIDString)} {lang.GetMessage("UISkillTree", this, player.UserIDString)}", Font = "robotocondensed-bold.ttf", FontSize = 26, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-180 190.4", OffsetMax = "180 250.4" }
                }
            });
            container.Add(new CuiElement
            {
                Name = "SkillTree_Points",
                Parent = "SkillTree",
                Components = {
                    new CuiTextComponent { Text = $"{lang.GetMessage("UITreePointsSpent", this, player.UserIDString)} <color=#4214388>{ti.trees[tree].points_spent}</color>", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-137 139.8", OffsetMax = "4.788 159.8" }
                }
            });
            var totalPoints = 0;
            foreach (var t in ti.trees)
            {
                totalPoints += t.Value.points_spent;
            }
            var ttps = $"{lang.GetMessage("UITotalPointsSpent", this, player.UserIDString)} <color=#4214388>{totalPoints}</color>";
            var max_skill_points = GetMaxSkillPoints(player);
            if (max_skill_points > 0) ttps = $"{lang.GetMessage("UITotalPointsSpent", this, player.UserIDString)} <color=#4214388>{totalPoints}/{max_skill_points}</color>";
            container.Add(new CuiElement
            {
                Name = "SkillTree_Points_global",
                Parent = "SkillTree",
                Components = {
                    new CuiTextComponent { Text = ttps, Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "4.788 139.8", OffsetMax = "146.578 159.8" }
                }
            });
            Dictionary<string, NodeInfo> T1_Nodes = new Dictionary<string, NodeInfo>();
            Dictionary<string, NodeInfo> T2_Nodes = new Dictionary<string, NodeInfo>();
            Dictionary<string, NodeInfo> T3_Nodes = new Dictionary<string, NodeInfo>();
            KeyValuePair<string, NodeInfo> Ultimate_node = new KeyValuePair<string, NodeInfo>();
            foreach (var n in ni.nodes)
            {
                if (n.Value.tier == 1) T1_Nodes.Add(n.Key, n.Value);
                else if (n.Value.tier == 2) T2_Nodes.Add(n.Key, n.Value);
                else if (n.Value.tier == 3) T3_Nodes.Add(n.Key, n.Value);
                else if (n.Value.tier == 4) Ultimate_node = new KeyValuePair<string, NodeInfo>(n.Key, n.Value);
            }
            var furthest_x = 0;
            List<int> largest = Pool.GetList<int>();
            largest.Add(T1_Nodes.Count);
            largest.Add(T2_Nodes.Count);
            largest.Add(T3_Nodes.Count);
            var biggest = largest.Max();

            Pool.FreeList(ref largest);
            var space = biggest <= 4 ? 50 : biggest == 5 ? 30 : biggest == 6 ? 15 : 4;
            ulong skinID;
            for (int i = 0; i < T1_Nodes.Count; i++)
            {
                var node = T1_Nodes.ElementAt(i);
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1698113 0.1698113 0.1698113 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{-137 + (i * (space + 58))} {59}", OffsetMax = $"{-79 + (i * (space + 58))} {117}" }
                }, "SkillTree", "SkillTree_panel_1");
                
                if (config.general_settings.image_cache_source.Equals("url", StringComparison.OrdinalIgnoreCase) || (NodeSkinDirectory.TryGetValue(node.Key, out skinID) && skinID == 0))
                {
                    container.Add(new CuiElement
                    {
                        Name = "SkillTree_img_1",
                        Parent = "SkillTree_panel_1",
                        Components = {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = (string)ImageLibrary?.Call("GetImage", node.Key) },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-27 -27", OffsetMax = "27 27" }
                    }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Name = "SkillTree_img_1",
                        Parent = "SkillTree_panel_1",
                        Components = {
                        new CuiImageComponent { Color = "1 1 1 1", ItemId = 1751045826, SkinId = skinID },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-27 -27", OffsetMax = "27 27" }
                    }
                    });
                }
               

                container.Add(new CuiElement
                {
                    Name = "SkillTree_points_assigned_1",
                    Parent = "SkillTree_panel_1",
                    Components = {
                    new CuiTextComponent { Text = node.Value.level_current.ToString(), Font = "robotocondensed-regular.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-27 -27", OffsetMax = "0 0" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 1 1 0", Command = $"stsendsubmenu {tree} {node.Key}" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-27 -27", OffsetMax = "27 27" }
                }, "SkillTree_panel_1", "SkillTree_button_1");

                if (-79 + (i * (space + 58)) > furthest_x) furthest_x = -79 + (i * (space + 58));
            }

            for (int i = 0; i < T2_Nodes.Count; i++)
            {
                var node = T2_Nodes.ElementAt(i);
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1698113 0.1698113 0.1698113 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{-137 + (i * (space + 58))} {-29}", OffsetMax = $"{-79 + (i * (space + 58))} {29}" }
                }, "SkillTree", "SkillTree_panel_5");

                if (config.general_settings.image_cache_source.Equals("url", StringComparison.OrdinalIgnoreCase) || (NodeSkinDirectory.TryGetValue(node.Key, out skinID) && skinID == 0))
                {
                    container.Add(new CuiElement
                    {
                        Name = "SkillTree_img_5",
                        Parent = "SkillTree_panel_5",
                        Components = {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = (string)ImageLibrary?.Call("GetImage", node.Key) },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-27 -27", OffsetMax = "27 27" }
                    }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Name = "SkillTree_img_5",
                        Parent = "SkillTree_panel_5",
                        Components = {
                        new CuiImageComponent { Color = "1 1 1 1", ItemId = 1751045826, SkinId = skinID },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-27 -27", OffsetMax = "27 27" }
                    }
                    });
                }

                

                if ((ni.points_spent < config.general_settings.t2_points_required))
                {
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = false,
                        Image = { Color = "0.3584906 0.3584906 0.3584906 0.8823529" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-27 -27", OffsetMax = "27 27" }
                    }, "SkillTree_panel_5", "SkillTree_Grey_Box_5");
                }

                container.Add(new CuiElement
                {
                    Name = "SkillTree_points_assigned_5",
                    Parent = "SkillTree_panel_5",
                    Components = {
                    new CuiTextComponent { Text = node.Value.level_current.ToString(), Font = "robotocondensed-regular.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-27 -27", OffsetMax = "0 0" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 1 1 0", Command = $"stsendsubmenu {tree} {node.Key}" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-27 -27", OffsetMax = "27 27" }
                }, "SkillTree_panel_5", "SkillTree_button_5");

                if (-79 + (i * (space + 58)) > furthest_x) furthest_x = -79 + (i * (space + 58));
            }    
            
            for (int i = 0; i < T3_Nodes.Count; i++)
            {
                var node = T3_Nodes.ElementAt(i);
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1698113 0.1698113 0.1698113 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{-137 + (i * (space + 58))} {-117}", OffsetMax = $"{-79 + (i * (space + 58))} {-59}" }
                }, "SkillTree", "SkillTree_panel_8");

                if (config.general_settings.image_cache_source.Equals("url", StringComparison.OrdinalIgnoreCase) || (NodeSkinDirectory.TryGetValue(node.Key, out skinID) && skinID == 0))
                {
                    container.Add(new CuiElement
                    {
                        Name = "SkillTree_img_8",
                        Parent = "SkillTree_panel_8",
                        Components = {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = (string)ImageLibrary?.Call("GetImage", node.Key) },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-27 -27", OffsetMax = "27 27" }
                    }
                    });
                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Name = "SkillTree_img_8",
                        Parent = "SkillTree_panel_8",
                        Components = {
                        new CuiImageComponent { Color = "1 1 1 1", ItemId = 1751045826, SkinId = skinID },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-27 -27", OffsetMax = "27 27" }
                    }
                    });
                }

                    

                if (ni.points_spent < config.general_settings.t3_points_required)
                {
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = false,
                        Image = { Color = "0.3584906 0.3584906 0.3584906 0.8823529" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-27 -27", OffsetMax = "27 27" }
                    }, "SkillTree_panel_8", "SkillTree_Grey_Box_8");
                }

                container.Add(new CuiElement
                {
                    Name = "SkillTree_points_assigned_8",
                    Parent = "SkillTree_panel_8",
                    Components = {
                    new CuiTextComponent { Text = node.Value.level_current.ToString(), Font = "robotocondensed-regular.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-27 -27", OffsetMax = "0 0" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 1 1 0", Command = $"stsendsubmenu {tree} {node.Key}" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-27 -27", OffsetMax = "27 27" }
                }, "SkillTree_panel_8", "SkillTree_button_8");

                if (-79 + (i * (space + 58)) > furthest_x) furthest_x = -79 + (i * (space + 58));
            }

            List<string> categories = Pool.GetList<string>();
            categories.AddRange(ti.trees.Keys);

            if (categories.Count > 1 && categories.IndexOf(tree) > 0)
            {
                var prevTree = categories[categories.IndexOf(tree) - 1];
                container.Add(new CuiElement
                {
                    Name = "SkillTree_Back",
                    Parent = "SkillTree",
                    Components = {
                    new CuiTextComponent { Text = lang.GetMessage("UIBackArrow", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-137 -254.67", OffsetMax = "-79 -222.67" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 1 1 0", Command = $"stmenuchangepage {prevTree}" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-29 -16", OffsetMax = "29 16" }
                }, "SkillTree_Back", "SkillTree_back_button");
            }
            if (categories.Count > 1 && categories.IndexOf(tree) < categories.Count - 1)
            {
                var nextTree = categories[categories.IndexOf(tree) + 1];
                container.Add(new CuiElement
                {
                    Name = "SkillTree_forward",
                    Parent = "SkillTree",
                    Components = {
                    new CuiTextComponent { Text = lang.GetMessage("UINextArrow", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "79 -254.67", OffsetMax = "137 -222.67" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 1 1 0", Command = $"stmenuchangepage {nextTree}" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-29 -16", OffsetMax = "29 16" }
                }, "SkillTree_forward", "SkillTree_forward_button");
            }
            Pool.FreeList(ref categories);
            container.Add(new CuiElement
            {
                Name = "SkillTree_close",
                Parent = "SkillTree",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("UIClose", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-29 -254.67", OffsetMax = "29 -222.67" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = $"stmenuclosemain" },
                Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-29 -16", OffsetMax = "29 16" }
            }, "SkillTree_close", "SkillTree_close_button");

            if (foundNode != null)
            {
                container.Add(new CuiElement
                {
                    Name = "SkillTree_node_name",
                    Parent = "SkillTree",
                    Components = {
                    new CuiTextComponent { Text = string.Format(lang.GetMessage("UISelectedNode", this, player.UserIDString), lang.GetMessage(selected_node, this, player.UserIDString)), Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-450.04 127.8", OffsetMax = "-233.36 159.8" }
                }
                });

                string nodeDescription = foundNode.description;

                if (config.trees[tree].nodes[selected_node].permissions != null)
                {
                    nodeDescription += config.trees[tree].nodes[selected_node].permissions.description;
                }

                container.Add(new CuiElement
                {
                    Name = "SkillTree_node_description",
                    Parent = "SkillTree",
                    Components = {
                    new CuiTextComponent { Text = nodeDescription, Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-450.037 0", OffsetMax = "-233.363 105.3" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "SkillTree_node_max_level",
                    Parent = "SkillTree",
                    Components = {
                    new CuiTextComponent { Text = string.Format(lang.GetMessage("UIMaxLevel", this, player.UserIDString), foundNode.level_max), Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-450.037 -29", OffsetMax = "-233.363 -5.982" }
                }
                });
                if (foundNode.level_max != foundNode.level_current)
                {
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = false,
                        Image = { Color = "0.2358491 0.2358491 0.2358491 0.6176471" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-291.36 -33.491", OffsetMax = "-233.36 -1.491" }
                    }, "SkillTree", "Panel_3082");

                    container.Add(new CuiElement
                    {
                        Name = "SkillTree_level",
                        Parent = "SkillTree",
                        Components = {
                    new CuiTextComponent { Text = "<color=#ffb600>Level Up</color>", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-291.36 -33.491", OffsetMax = "-233.36 -1.491" }
                    }
                    });

                    container.Add(new CuiButton
                    {
                        Button = { Color = "1 1 1 0", Command = $"stdolevel {tree} {selected_node}" },
                        Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-29 -16", OffsetMax = "29 16" }
                    }, "SkillTree_level", "SkillTree_level_button");
                }
            }
            container.Add(new CuiElement
            {
                Name = "SkillTree_Points_available",
                Parent = "SkillTree",
                Components = {
                    new CuiTextComponent { Text = $"{lang.GetMessage("UIAvailablePoints", this, player.UserIDString)} <color=#4214388>{pcdData.pEntity[player.userID].available_points}</color>", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-450.04 -190.019", OffsetMax = "-284.04 -167.001" }
                }
            });
            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values != null)
            {
                container.Add(new CuiElement
                {
                    Name = "SkillTree_buff_totals",
                    Parent = "SkillTree",
                    Components = {
                    new CuiTextComponent { Text = lang.GetMessage("UIBuffInformation", this, player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{(furthest_x + 36 > 172.59 ? furthest_x + 36 : 172.59)} 127.8", OffsetMax = $"{(furthest_x + 36 + 166 > 338.59 ? furthest_x + 36 + 166 : 338.59)} 159.8" }
                }
                });
                StringBuilder sb = new StringBuilder("");
                StringBuilder sb2 = new StringBuilder("");
                var loopCount = 0;
                foreach (var buff in bd.buff_values)
                {
                    if (buff.Value == 0) continue;
                    if (BuffBuffType[buff.Key] == BuffType.Permission) continue;
                    string value = "";
                    if (BuffBuffType.ContainsKey(buff.Key))
                    {
                        if (BuffBuffType[buff.Key] == BuffType.Percentage) value = $"+{Math.Round(buff.Value / 1 * 100, 2)}%";
                        else if (BuffBuffType[buff.Key] == BuffType.Seconds) value = $"-{buff.Value} seconds";
                        else if (BuffBuffType[buff.Key] == BuffType.PerSecond) value = $"+{buff.Value} / second";
                        else if (BuffBuffType[buff.Key] == BuffType.Slots) value = $"+{buff.Value} slots";
                        else value = "Enabled";
                    }
                    if (loopCount < 38) sb.AppendFormat("{0} - <color=#4214388>{1}</color>\n", lang.GetMessage("UI" + buff.Key.ToString(), this, player.UserIDString), value);
                    else sb2.AppendFormat("{0} - <color=#4214388>{1}</color>\n", lang.GetMessage("UI" + buff.Key.ToString(), this, player.UserIDString), value);

                    loopCount++;
                }
                Dictionary<string, string> perms = new Dictionary<string, string>();
                if (Tracked_perms.ContainsKey(player.UserIDString))
                {
                    foreach (var list in Tracked_perms[player.UserIDString])
                    {
                        foreach (var entry in list.Value)
                        {
                            if (!perms.ContainsKey(entry.Key)) perms.Add(entry.Key, entry.Value);
                        }
                    }
                }
                
                int count = sb.ToString().Split('\n').Length;
                foreach (var perm in perms)
                {
                    if (count <= 38) sb.AppendFormat("Perm - <color=#4214388>{0}</color>\n", perm.Value ?? perm.Key);
                    else sb2.AppendFormat("Perm - <color=#4214388>{0}</color>\n", perm.Value ?? perm.Key);
                    count++;
                }
                //Tracked_perms
                container.Add(new CuiElement
                {
                    Name = "SkillTree_Buffs_displayed",
                    Parent = "SkillTree",
                    Components = {
                    new CuiTextComponent { Text = sb.ToString(), Font = "robotocondensed-regular.ttf", FontSize = 8, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{(furthest_x + 36 > 172.585 ? furthest_x + 36 : 172.585)} -295.889", OffsetMax = $"{(furthest_x + 36 + 143.704f > 316.289 ? furthest_x + 36 + 143.704f : 316.289)} 117" }
                }
                });
                container.Add(new CuiElement
                {
                    Name = "SkillTree_Buffs_displayed_second",
                    Parent = "SkillTree",
                    Components = {
                    new CuiTextComponent { Text = sb2.ToString(), Font = "robotocondensed-regular.ttf", FontSize = 8, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{(furthest_x + 36 + 143.704f > 316.289 ? furthest_x + 36 + 143.704f : 316.289)} -295.89", OffsetMax = $"{(furthest_x + 36 + 287.408f > 459.992 ? furthest_x + 36 + 287.408f : 459.992)} 117" }
                }
                });
                perms.Clear();
                if (config.general_settings.allow_respecs)
                {
                    var cost = (double)0;
                    var formatted = "";
                    var respec_cost = GetRespecCost(player);
                    if (respec_cost > 0) cost = Math.Round(totalPoints * respec_cost, 2);
                    if (config.general_settings.respec_currency.Equals("scrap", StringComparison.OrdinalIgnoreCase)) formatted = $"{cost} {lang.GetMessage("UIScrap", this, player.UserIDString)}";
                    else if (config.general_settings.respec_currency.Equals("economics", StringComparison.OrdinalIgnoreCase)) formatted = $"{lang.GetMessage("UIDollars", this, player.UserIDString)}{cost}";
                    else if (config.general_settings.respec_currency.Equals("srp", StringComparison.OrdinalIgnoreCase)) formatted = $"{cost} {lang.GetMessage("UIPoints", this, player.UserIDString)}";
                    else if (config.general_settings.respec_currency.Equals("custom", StringComparison.OrdinalIgnoreCase)) formatted = $"{cost} {config.general_settings.respec_currency_custom.displayName}";
                    else formatted = cost.ToString();
                    container.Add(new CuiElement
                    {
                        Name = "SkillTree_Respec_cost_title",
                        Parent = "SkillTree",
                        Components = {
                        new CuiTextComponent { Text = string.Format(lang.GetMessage("RespecCost", this, player.UserIDString), formatted), Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                        new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-450.04 -77.108", OffsetMax = "-291.36 -54.091" }
                    }
                    });
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = false,
                        Image = { Color = "0.2358491 0.2358491 0.2358491 0.6176471" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-291.36 -81.6", OffsetMax = "-233.36 -49.6" }
                    }, "SkillTree", "SkillTree_Respec_cost_button_panel");

                    container.Add(new CuiElement
                    {
                        Name = "SkillTree_Respec_cost_label",
                        Parent = "SkillTree_Respec_cost_button_panel",
                        Components = {
                        new CuiTextComponent { Text = lang.GetMessage("RespecButton", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                        new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-29 -16", OffsetMax = "29 16" }
                    }
                    });
                    container.Add(new CuiButton
                    {
                        Button = { Color = "1 1 1 0", Command = $"respecconfirmation {cost} {tree} {selected_node}" },
                        Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-29 -16", OffsetMax = "29 16" }
                    }, "SkillTree_Respec_cost_label", "SkillTree_Respec_cost_button");
                }
            }
            PlayerInfo playerData;

            if (pcdData.pEntity.TryGetValue(player.userID, out playerData))
            {
                if (config.rested_xp_settings.rested_xp_enabled)
                {
                    container.Add(new CuiElement
                    {
                        Name = "SkillTree_Player_Rested_XP",
                        Parent = "SkillTree",
                        Components = {
                    new CuiTextComponent { Text = $"{lang.GetMessage("UIRestedXPPool", this, player.UserIDString)} <color=#03b2d9>{Math.Round(playerData.xp_bonus_pool, config.xp_settings.xp_rounding)}</color>", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-450.04 -117.619", OffsetMax = "-291.36 -94.602" }
                    }
                    });
                }               

                var level = config.general_settings.max_player_level > 0 && playerData.current_level > config.general_settings.max_player_level ? config.general_settings.max_player_level : playerData.current_level;
                container.Add(new CuiElement
                {
                    Name = "SkillTree_Player_Level",
                    Parent = "SkillTree",
                    Components = {
                    new CuiTextComponent { Text = $"{lang.GetMessage("UICurrentLevel", this, player.UserIDString)} <color=#03b2d9>{level}</color>", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-450.04 -142.309", OffsetMax = "-291.36 -119.292" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "SkillTree_Player_XP",
                    Parent = "SkillTree",
                    Components = {
                    new CuiTextComponent { Text = $"{lang.GetMessage("UIXP", this, player.UserIDString)} <color=#03b2d9>{Math.Round(playerData.xp, 2)}</color>/<color=#03b2d9>{Math.Round(config.level.GetLevelStartXP(playerData.current_level + 1), config.xp_settings.xp_rounding)}</color>", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-450.04 -166.999", OffsetMax = "-188.512 -143.982" }
                }
                });
            }

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.2358491 0.2358491 0.2358491 0.3176471" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-450.04 -254.67", OffsetMax = "-321.646 -222.67" }
            }, "SkillTree", "SkillTree_player_settings_panel");

            container.Add(new CuiElement
            {
                Name = "SkillTree_player_settings_text",
                Parent = "SkillTree_player_settings_panel",
                Components = {
                    new CuiTextComponent { Text = $"<color=#ffb600>{lang.GetMessage("ButtonPlayerSettings", this, player.UserIDString)}</color>", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-64.195 -16", OffsetMax = "64.195 16" }
                }
            });
            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = $"stsendplayersettingsmenu" },
                Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-64.195 -16", OffsetMax = "64.195 16" }
            }, "SkillTree_player_settings_text", "SkillTree_player_settings_button");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.2358491 0.2358491 0.2358491 0.3176471" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-311.647 -254.67", OffsetMax = "-183.253 -222.67" }
            }, "SkillTree", "SkillTree_ultimate_settings_panel");

            container.Add(new CuiElement
            {
                Name = "SkillTree_ultimate_settings_text",
                Parent = "SkillTree_ultimate_settings_panel",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("UltimateSettings", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-64.195 -16", OffsetMax = "64.195 16" }
                }
            });
            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = $"stsendultimatesettingsmenu" },
                Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-64.195 -16", OffsetMax = "64.195 16" }
            }, "SkillTree_ultimate_settings_text", "SkillTree_ultimate_settings_button");

            if (Ultimate_node.Key != null && Ultimate_node.Value != null)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = config.ultimate_settings.ultimate_node_background_col },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{(biggest <= 4 ? -29 : -137)} -195", OffsetMax = $"{(biggest <= 4 ? 29 : -79)} -137" }
                }, "SkillTree", "SkillTree_panel_ultimate");

                if (config.general_settings.image_cache_source.Equals("url", StringComparison.OrdinalIgnoreCase) || (NodeSkinDirectory.TryGetValue(Ultimate_node.Key, out skinID) && skinID == 0))
                {
                    container.Add(new CuiElement
                    {
                        Name = "SkillTree_img_ultimate",
                        Parent = "SkillTree_panel_ultimate",
                        Components = {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = (string)ImageLibrary?.Call("GetImage", Ultimate_node.Key) },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-27 -27", OffsetMax = "27 27" }
                    }
                    });

                }
                else
                {
                    container.Add(new CuiElement
                    {
                        Name = "SkillTree_img_ultimate",
                        Parent = "SkillTree_panel_ultimate",
                        Components = {
                        new CuiImageComponent { Color = "1 1 1 1", ItemId = 1751045826, SkinId = skinID },
                        new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-27 -27", OffsetMax = "27 27" }
                    }
                    });
                }
                    

                if (ni.points_spent < config.general_settings.ultimate_points_required)
                {
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = false,
                        Image = { Color = "0.3584906 0.3584906 0.3584906 0.8823529" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-27 -27", OffsetMax = "27 27" }
                    }, "SkillTree_panel_ultimate", "SkillTree_Grey_Box_ultimate");

                }

                container.Add(new CuiElement
                {
                    Name = "SkillTree_points_assigned_ultimate",
                    Parent = "SkillTree_panel_ultimate",
                    Components = {
                    new CuiTextComponent { Text = Ultimate_node.Value.level_current.ToString(), Font = "robotocondensed-regular.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-27 -27", OffsetMax = "0 0" }
                }
                });
                container.Add(new CuiButton
                {
                    Button = { Color = "1 1 1 0", Command = $"stsendsubmenu {tree} {Ultimate_node.Key}" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-27 -27", OffsetMax = "27 27" }
                }, "SkillTree_panel_ultimate", "SkillTree_button_ultimate");
            }            

            CuiHelper.DestroyUi(player, "SkillTree");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("stsendplayersettingsmenu")]
        void SendPlayerSettings(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            SkillTree_PlayerMenu(player);
        }

        [ConsoleCommand("stsendultimatesettingsmenu")]
        void SendUltimateSettingsMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            SkillTree_UltimateMenu(player);
        }

        #endregion

        #region XPMenu

        string UnmodifiedCol;
        string ModifiedCol;

        void DisplayXPMenu(BasePlayer player, double xp, bool modified)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = "XP_Tick",
                Parent = "ContentUI",
                Components = {
                    new CuiTextComponent { Text = string.Format(lang.GetMessage("popupxpstring", this, player.UserIDString), modified ? ModifiedCol : UnmodifiedCol, Math.Round(xp, config.xp_settings.xp_rounding)), Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-26.83 244.369", OffsetMax = "26.83 268.631" }
                }
            });

            CuiHelper.DestroyUi(player, "XP_Tick");
            CuiHelper.AddUi(player, container);

            timer.Once(config.xp_settings.xp_display_time, () => CuiHelper.DestroyUi(player, "XP_Tick"));
        }

        #endregion

        #region respec confirmation menu

        void ConfirmRespec(BasePlayer player, double cost, string tree, string selected_node)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0.9907843" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0.011 0.009", OffsetMax = "0.011 0.339" }
            }, "ContentUI", "respec_confirmation");

            string text = lang.GetMessage("UIAreYouSure", this, player.UserIDString);
            if (config.general_settings.respec_multiplier > 0)
            {
                PlayerInfo pi;
                if (pcdData.pEntity.TryGetValue(player.userID, out pi)) text += string.Format(lang.GetMessage("RespecMultiplierMessage", this, player.UserIDString), Mathf.Min(pi.respec_multiplier + config.general_settings.respec_multiplier, config.general_settings.respec_multiplier_max) * 100);
            }

            container.Add(new CuiElement
            {
                Name = "respec_confirmation_title",
                Parent = "respec_confirmation",
                Components = {
                    new CuiTextComponent { Text = text, Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-117.039 63.5", OffsetMax = "117.039 163.5" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.2358491 0.2358491 0.2358491 0.3176471" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-117.039 -16", OffsetMax = "-59.039 16" }
            }, "respec_confirmation", "respec_confirmation_bttn_panel_yes");

            container.Add(new CuiElement
            {
                Name = "respec_confirmation_bttn_text_yes",
                Parent = "respec_confirmation_bttn_panel_yes",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("ButtonYes", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-29 -16", OffsetMax = "29 16" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = $"dorespec {cost} {tree} {selected_node}" },
                Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-29 -16", OffsetMax = "29 16" }
            }, "respec_confirmation_bttn_text_yes", "respec_confirmation_bttn_yes");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.2358491 0.2358491 0.2358491 0.3176471" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "59.039 -16", OffsetMax = "117.039 16" }
            }, "respec_confirmation", "respec_confirmation_bttn_panel_no");

            container.Add(new CuiElement
            {
                Name = "respec_confirmation_bttn_text_no",
                Parent = "respec_confirmation_bttn_panel_no",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("ButtonNo", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-29 -16", OffsetMax = "29 16" }
                }
            });

            var cost_string = lang.GetMessage("UICost", this, player.UserIDString);
            if (config.general_settings.respec_currency.Equals("scrap", StringComparison.OrdinalIgnoreCase)) cost_string = string.Format(cost_string, $"{cost} {lang.GetMessage("UIScrap", this, player.UserIDString)}");
            else if (config.general_settings.respec_currency.Equals("economics", StringComparison.OrdinalIgnoreCase)) cost_string = string.Format(cost_string, $"{lang.GetMessage("UIDollars", this, player.UserIDString)}{cost}");
            else if (config.general_settings.respec_currency.Equals("srp", StringComparison.OrdinalIgnoreCase)) cost_string = string.Format(cost_string, $"{cost} {lang.GetMessage("UIPoints", this, player.UserIDString)}");
            else if (config.general_settings.respec_currency.Equals("custom", StringComparison.OrdinalIgnoreCase)) cost_string = string.Format(cost_string, $"{cost} {config.general_settings.respec_currency_custom.displayName}");

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = $"closerecpecconfirmation {tree} {selected_node}" },
                Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-29 -16", OffsetMax = "29 16" }
            }, "respec_confirmation_bttn_text_no", "respec_confirmation_bttn_no");

            container.Add(new CuiElement
            {
                Name = "respec_confirmation_cost",
                Parent = "respec_confirmation",
                Components = {
                    new CuiTextComponent { Text = cost_string, Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-68.137 28.482", OffsetMax = "68.137 63.5" }
                }
            });

            CuiHelper.DestroyUi(player, "respec_confirmation");
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Menu commands

        [ConsoleCommand("respecconfirmation")]
        void SendConfirmation(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            var cost = Convert.ToDouble(arg.Args[0]);
            var tree = arg.Args[1];
            var name = string.Join(" ", arg.Args.Skip(2));
            //CuiHelper.DestroyUi(player, "SkillTree");
            ConfirmRespec(player, cost, tree, name);
        }

        [ConsoleCommand("closerecpecconfirmation")]
        void CloseRespecConfirmation(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            CuiHelper.DestroyUi(player, "respec_confirmation");
            //var tree = Convert.ToInt32(arg.Args[0]);
            //if (arg.Args.Length == 1) SendSkillTreeMenu(player, (string)tree);
            //else
            //{
            //    var name = string.Join(" ", arg.Args.Where(x => !x.IsNumeric()));
            //    SendSkillTreeMenu(player, (string)tree, name);
            //}            
        }

        [ConsoleCommand("stsendsubmenu")]
        void SendSubMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            var tree = arg.Args[0];
            var name = string.Join(" ", arg.Args.Skip(1));
            SendSkillTreeMenu(player, tree, name);
        }

        [ConsoleCommand("stmenuclosemain")]
        void CloseMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            CuiHelper.DestroyUi(player, "SkillTree");
            CuiHelper.DestroyUi(player, "SkillTreeBackPanel");
        }

        [ConsoleCommand("stmenuchangepage")]
        void SendNextPage(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            var tree = arg.Args[0];
            SendSkillTreeMenu(player, tree);
        }

        [ConsoleCommand("stdolevel")]
        void DoLevel(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            var tree = arg.Args[0];
            var name = string.Join(" ", arg.Args.Skip(1));
            LevelUpNode(player, tree, name);
            SendSkillTreeMenu(player, tree, name);
        }

        [ConsoleCommand("dorespec")]
        void DoRespec(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            CuiHelper.DestroyUi(player, "respec_confirmation");
            var cost = Convert.ToDouble(arg.Args[0]);
            if (cost > 0)
            {
                if (config.general_settings.respec_currency.Equals("scrap", StringComparison.OrdinalIgnoreCase))
                {
                    var found = 0;
                    foreach (var item in player.inventory.AllItems())
                    {
                        if (item.info.shortname == "scrap") found += item.amount;
                        if (found >= cost) break;
                    }
                    if (found < cost)
                    {
                        PrintToChat(player, lang.GetMessage("RespecNoScrap", this, player.UserIDString));
                        return;
                    }
                    found = 0;
                    foreach (var item in player.inventory.AllItems())
                    {
                        if (item.info.shortname != "scrap") continue;
                        if (item.amount + found == cost)
                        {
                            item.Remove();
                            break;
                        }
                        if (item.amount + found < cost)
                        {
                            found += item.amount;
                            item.Remove();
                        }
                        else
                        {
                            item.UseItem(Convert.ToInt32(cost) - found);
                            break;
                        }
                    }
                }
                else if (config.general_settings.respec_currency.Equals("economics", StringComparison.OrdinalIgnoreCase))
                {
                    if (Economics == null)
                    {
                        PrintToChat(player, lang.GetMessage("EconNotLoaded", this, player.UserIDString));
                        return;
                    }
                    var playerBalance = Convert.ToDouble(Economics?.Call("Balance", player.userID));
                    if (playerBalance < cost)
                    {
                        PrintToChat(player, lang.GetMessage("EconNoCash", this, player.UserIDString));
                        return;
                    }
                    if (!Convert.ToBoolean(Economics?.Call("Withdraw", player.userID, cost)))
                    {
                        PrintToChat(player, lang.GetMessage("EconErrorCash", this, player.UserIDString));
                        return;
                    }
                }
                else if (config.general_settings.respec_currency.Equals("srp", StringComparison.OrdinalIgnoreCase))
                {
                    if (ServerRewards == null)
                    {
                        PrintToChat(player, lang.GetMessage("SRNotLoaded", this, player.UserIDString));
                        return;
                    }
                    var balance = Convert.ToInt32(ServerRewards.Call("CheckPoints", player.userID));
                    if (balance < cost)
                    {
                        PrintToChat(player, lang.GetMessage("SRNoPoints", this, player.UserIDString));
                        return;
                    }
                    var intCost = Convert.ToInt32(cost);
                    if (!Convert.ToBoolean(ServerRewards?.Call("TakePoints", player.userID, intCost)))
                    {
                        PrintToChat(player, lang.GetMessage("SRPointError", this, player.UserIDString));
                        return;
                    }
                }
                else if (config.general_settings.respec_currency.Equals("custom", StringComparison.OrdinalIgnoreCase))
                {
                    var found = 0;
                    foreach (var item in player.inventory.AllItems())
                    {
                        if (item.skin == config.general_settings.respec_currency_custom.skin && item.info.shortname == config.general_settings.respec_currency_custom.shortname) found += item.amount;
                        if (found > cost) break;
                    }
                    if (found < cost)
                    {
                        PrintToChat(player, string.Format(lang.GetMessage("RespecNoCustom", this, player.UserIDString), config.general_settings.respec_currency_custom.displayName));
                        return;
                    }
                    found = 0;
                    foreach (var item in player.inventory.AllItems())
                    {
                        if (item.skin != config.general_settings.respec_currency_custom.skin || item.info.shortname != config.general_settings.respec_currency_custom.shortname) continue;
                        if (item.amount + found == cost)
                        {
                            item.Remove();
                            break;
                        }
                        if (item.amount + found < cost)
                        {
                            found += item.amount;
                            item.Remove();
                        }
                        else
                        {
                            item.UseItem(Convert.ToInt32(cost) - found);
                            break;
                        }
                    }
                }
            }
            var tree = arg.Args[1];
            var name = "";
            if (arg.Args.Length > 2)
            {
                name = string.Join(" ", arg.Args.Skip(2));
            }
            if (string.IsNullOrEmpty(name)) name = null;

            if (config.general_settings.respec_multiplier > 0)
            {
                PlayerInfo pi;
                if (pcdData.pEntity.TryGetValue(player.userID, out pi))
                {
                    pi.respec_multiplier += config.general_settings.respec_multiplier;
                    if (config.general_settings.respec_multiplier_max > 0 && pi.respec_multiplier > config.general_settings.respec_multiplier_max) pi.respec_multiplier = config.general_settings.respec_multiplier_max;
                }
            }
            
            RespecPlayer(player);
            SendSkillTreeMenu(player, tree, name);
            PrintToChat(player, string.Format(lang.GetMessage("PaidRespec", this, player.UserIDString), cost));
        }

        #endregion

        #region Chat commands        

        const string perm_admin = "skilltree.admin";
        const string perm_no_scoreboard = "skilltree.noscoreboard";

        [ChatCommand("resetxpbars")]
        void ResetXPBars(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, perm_admin)) return;

            foreach (var kvp in pcdData.pEntity)
            {
                kvp.Value.xp_hud_pos = config.general_settings.pump_bar_settings.offset_default;                
            }
            
            foreach (var p in BasePlayer.activePlayerList)
            {
                PlayerInfo pi;
                if (!pcdData.pEntity.TryGetValue(p.userID, out pi)) continue;
                UpdateXP(p, pi);
            }

            PrintToChat(player, "Reset all xp bars to default settings.");
        }

        [ChatCommand("resetxpbar")]
        void ResetXPBar(BasePlayer player)
        {
            PlayerInfo pi;
            if (!pcdData.pEntity.TryGetValue(player.userID, out pi))
            {
                PrintToChat(player, "No data detected. Please reconnect to the server.");
                return;
            }

            pi.xp_hud_pos = config.general_settings.pump_bar_settings.offset_default;
            UpdateXP(player, pi);

            PrintToChat(player, "Your xp bar has been reset to default settings.");
        }

        [ChatCommand("resetallresteddata")]
        void ResetAllRestedData(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, perm_admin)) return;

            foreach (var kvp in pcdData.pEntity)
            {
                kvp.Value.xp_bonus_pool = 0;
                kvp.Value.logged_off = DateTime.Now;                
            }
        }

        [ChatCommand("movebar")]
        void MoveBar(BasePlayer player)
        {
            SendXPBarMoverMenu(player);
        }

        void SendMenuCMD(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, "skilltree.chat"))
            {
                PrintToChat(player, lang.GetMessage("NoPermsChat", this, player.UserIDString));
                return;
            }            
            SkillTreeBackPanel(player);
            SendSkillTreeMenu(player);
        }

        [ChatCommand("togglebc")]
        void ToggleBetterChat(BasePlayer player)
        {
            PlayerInfo playerData;
            if (!pcdData.pEntity.TryGetValue(player.userID, out playerData))
            {
                //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {player.displayName}[{player.UserIDString}] - ToggleBetterChat. [Online = {player.IsConnected}]", this, true);
                if (player.IsConnected) PrintToChat(player, lang.GetMessage("FailReload", this, player.UserIDString));
                return;
            }
            if (playerData.better_chat_enabled)
            {
                PrintToChat(player, "Toggled better chat titles off.");
                playerData.better_chat_enabled = false;
            }
                
            else
            {
                PrintToChat(player, "Toggled better chat titles on.");
                playerData.better_chat_enabled = true;
            }
        }

        [ChatCommand("togglexpdrops")]
        void ToggleXPDrops(BasePlayer player)
        {
            PlayerInfo playerData;
            if (!pcdData.pEntity.TryGetValue(player.userID, out playerData))
            {
                //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {player.displayName}[{player.UserIDString}] - ToggleXPDrops. [Online = {player.IsConnected}]", this, true);
                if (player.IsConnected) PrintToChat(player, lang.GetMessage("FailReload", this, player.UserIDString));
                return;
            }
            if (playerData.xp_drops) playerData.xp_drops = false;
            else playerData.xp_drops = true;
        }

        [ChatCommand("togglexphud")]
        void ToggleXPHud(BasePlayer player)
        {
            PlayerInfo playerData;
            if (!pcdData.pEntity.TryGetValue(player.userID, out playerData))
            {
                //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {player.displayName}[{player.UserIDString}] - ToggleXPHud. [Online = {player.IsConnected}]", this, true);
                if (player.IsConnected) PrintToChat(player, lang.GetMessage("FailReload", this, player.UserIDString));
                return;
            }
            if (playerData.xp_hud)
            {
                playerData.xp_hud = false;
                CuiHelper.DestroyUi(player, "SkillTreeXPBar");
            }
            else
            {
                UpdateXP(player, playerData);
                playerData.xp_hud = true;
            }
        }

        [ChatCommand("sttogglenotifications")]
        void ToggleNotifications(BasePlayer player)
        {
            PlayerInfo playerData;
            if (!pcdData.pEntity.TryGetValue(player.userID, out playerData))
            {
                //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {player.displayName}[{player.UserIDString}] - ToggleNotifications. [Online = {player.IsConnected}]", this, true);
                if (player.IsConnected) PrintToChat(player, lang.GetMessage("FailReload", this, player.UserIDString));
                return;
            }
            if (playerData.notifications)
            {
                PrintToChat(player, lang.GetMessage("notificationsOff", this, player.UserIDString));
                playerData.notifications = false;
            }
                
            else
            {
                PrintToChat(player, lang.GetMessage("notificationsOn", this, player.UserIDString));
                playerData.notifications = true;
            }
                
        }

        [ConsoleCommand("givexp")]
        void GiveXPConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, "skilltree.admin")) return;

            if (arg.Args.Length == 0)
            {
                arg.ReplyWith(lang.GetMessage("GiveXPUsage", this, player.UserIDString));
                return;
            }
            if (!arg.Args.Last().IsNumeric())
            {
                arg.ReplyWith(lang.GetMessage("XPLastArg", this, player.UserIDString));
                return;
            }
            var xp = Convert.ToDouble(arg.Args.Last());
            var name = String.Join(" ", arg.Args.Take(arg.Args.Length - 1));
            var target = name.IsNumeric() ? FindPlayerByID(name, player ?? null) : FindPlayerByName(name, player ?? null);
            if (target == null) return;
            AwardXP(target, xp);
            PrintToChat(target, string.Format(lang.GetMessage("GaveXP", this, target.UserIDString), xp, player != null ? player.displayName : "Console"));
            arg.ReplyWith(string.Format(lang.GetMessage("ReceivedXP", this), target.displayName, xp));
        }

        [ChatCommand("givexp")]
        void GiveXPCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "skilltree.admin")) return;
            if (args.Length == 0)
            {
                PrintToChat(player, lang.GetMessage("GiveXPUsage", this, player.UserIDString));
                return;
            }
            if (!args.Last().IsNumeric())
            {
                PrintToChat(player, lang.GetMessage("XPLastArg", this, player.UserIDString));
                return;
            }
            var xp = Convert.ToDouble(args.Last());
            var name = String.Join(" ", args.Take(args.Length - 1));
            var target = FindPlayerByName(name, player);
            if (target == null) return;
            AwardXP(target, xp);
            PrintToChat(target, string.Format(lang.GetMessage("GaveXP", this, target.UserIDString), xp, player.displayName));
            PrintToChat(player, string.Format(lang.GetMessage("ReceivedXP", this, player.UserIDString), target.displayName, xp));
        }

        [ConsoleCommand("givesp")]
        void GiveSkillPointsConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, "skilltree.admin")) return;

            if (arg.Args.Length < 2 || !arg.Args.Last().IsNumeric())
            {
                arg.ReplyWith(lang.GetMessage("GiveSPUsage", this));
                return;
            }
            var amount = Convert.ToInt32(arg.Args.Last());
            var name = String.Join(" ", arg.Args.Take(arg.Args.Length - 1));
            var target = name.IsNumeric() ? FindPlayerByID(name, player ?? null) : FindPlayerByName(name, player ?? null);
            if (target == null) return;
            GiveSkillPoints(target, amount);
            arg.ReplyWith(string.Format(lang.GetMessage("GaveSP", this), amount, target.displayName));
        }


        [ChatCommand("givesp")]
        void GiveSkillPoints(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "skilltree.admin")) return;
            if (args.Length < 2 || !args.Last().IsNumeric())
            {
                PrintToChat(player, lang.GetMessage("GiveSPUsage", this, player.UserIDString));
                return;
            }
            var amount = Convert.ToInt32(args.Last());
            var target = FindPlayerByName(String.Join(" ", args.Take(args.Length - 1)), player);
            if (target == null) return;

            GiveSkillPoints(target, amount);
            PrintToChat(player, string.Format(lang.GetMessage("GaveSP", this, player.UserIDString), amount, target.displayName));
        }

        [ConsoleCommand("resetdata")]
        void ResetXPConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, "skilltree.admin")) return;

            if (arg.Args == null || arg.Args.Length == 0)
            {
                arg.ReplyWith(lang.GetMessage("ResetXPUsage", this));
                return;
            }
            var name = String.Join(" ", arg.Args);
            var target = name.IsNumeric() ? FindPlayerByID(name, player ?? null) : FindPlayerByName(name, player ?? null);
            if (target == null) return;
            DoClear(target);
            LoggingOff(target);
            PlayerInfo pi;
            if (pcdData.pEntity.TryGetValue(target.userID, out pi))
            {
                RunResetCommands(target.UserIDString, Math.Max(pi.current_level, pi.achieved_level));
                pcdData.pEntity.Remove(target.userID);
            }
                
            if (TreeData.ContainsKey(target.userID)) TreeData.Remove(target.userID);
            buffDetails.Remove(target.userID);
            HandleNewConnection(target);            
            arg.ReplyWith(string.Format(lang.GetMessage("ResetData", this), target.displayName));
            LoadBuffs();
        }

        [ChatCommand("resetdata")]
        void ResetXP(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "skilltree.admin")) return;
            if (args.Length == 0)
            {
                PrintToChat(player, lang.GetMessage("ResetXPUsage", this, player.UserIDString));
                return;
            }
            var target = FindPlayerByName(string.Join(" ", args), player);
            if (target == null) return;
            DoClear(target);
            LoggingOff(target);
            PlayerInfo pi;
            if (pcdData.pEntity.TryGetValue(target.userID, out pi))
            {
                RunResetCommands(target.UserIDString, Math.Max(pi.current_level, pi.achieved_level));
                pcdData.pEntity.Remove(target.userID);
            }
            if (TreeData.ContainsKey(target.userID)) TreeData.Remove(target.userID);
            buffDetails.Remove(target.userID);
            HandleNewConnection(target);            
            PrintToChat(player, string.Format(lang.GetMessage("ResetData", this, player.UserIDString), target.displayName));
            LoadBuffs();
        }

        [ConsoleCommand("stresetalldata")]
        void ResetAllDataConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, "skilltree.admin")) return;
            foreach (var id in BasePlayer.allPlayerList)
            {
                RemovePerms(id.UserIDString);
                PlayerInfo pi;
                if (pcdData.pEntity.TryGetValue(id.userID, out pi))
                {
                    RunResetCommands(id.UserIDString, Math.Max(pi.current_level, pi.achieved_level));
                }
            }
            buffDetails.Clear();
            pcdData.pEntity.Clear();
            TreeData.Clear();
            arg.ReplyWith("Reset all data.");
            if (BasePlayer.activePlayerList != null && BasePlayer.activePlayerList.Count > 0)
            {
                foreach (var p in BasePlayer.activePlayerList)
                {
                    HandleNewConnection(player);
                }
            }
            SaveData();
            LoadBuffs();
        }

        [ChatCommand("stresetalldata")]
        void ResetAllData(BasePlayer player = null)
        {
            if (player != null && !permission.UserHasPermission(player.UserIDString, "skilltree.admin")) return;
            foreach (var id in BasePlayer.allPlayerList)
            {
                RemovePerms(id.UserIDString);
                PlayerInfo pi;
                if (pcdData.pEntity.TryGetValue(id.userID, out pi))
                {
                    RunResetCommands(id.UserIDString, Math.Max(pi.current_level, pi.achieved_level));
                }
            }
            buffDetails.Clear();
            pcdData.pEntity.Clear();
            TreeData.Clear();
            if (player != null) PrintToChat(player, "Reset all data.");
            if (BasePlayer.activePlayerList != null && BasePlayer.activePlayerList.Count > 0)
            {
                foreach (var p in BasePlayer.activePlayerList)
                {
                    HandleNewConnection(player);
                }
            }
            SaveData();
            LoadBuffs();
        }

        [ConsoleCommand("strespecplayer")]
        void ResetSkillsConsoleSingle(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, "skilltree.admin")) return;

            if (pcdData.pEntity.Count == 0)
            {
                arg.ReplyWith(lang.GetMessage("NoPlayersSetup", this));
                return;
            }

            if (arg.Args == null || arg.Args.Length == 0)
            {
                arg.ReplyWith("Usage: strespecplayer <target name/ID>");
                return;
            }

            var target = BasePlayer.Find(arg.GetString(0));

            if (target != null)
            {
                RespecPlayer(target);
                arg.ReplyWith($"Reset skill points for {target.displayName}");
                if (target.IsConnected) PrintToChat(target, "Your skill points were reset.");
            }
            else
            {
                var ID = Convert.ToUInt64(string.Join(" ", arg.Args));
                if (!ID.IsSteamId())
                {
                    arg.ReplyWith($"{ID} is not a valid Steam ID");
                    return;
                }
                foreach (var p in BasePlayer.activePlayerList)
                {
                    if (p.userID == ID)
                    {
                        RespecPlayer(target);
                        arg.ReplyWith($"Reset skill points for {target.displayName}");
                        return;
                    }
                }
                buffDetails.Remove(ID);
                TreeData.Remove(ID);

                foreach (KeyValuePair<ulong, PlayerInfo> kvp in pcdData.pEntity)
                {
                    if (kvp.Key == ID)
                    {
                        int pointCount = 0;
                        foreach (var buff in kvp.Value.buff_values)
                        {
                            pointCount += buff.Value;
                        }
                        kvp.Value.available_points += pointCount;
                        if (config.general_settings.points_per_level * kvp.Value.current_level > kvp.Value.available_points) kvp.Value.available_points = config.general_settings.points_per_level * kvp.Value.current_level;
                        kvp.Value.buff_values.Clear();
                        RemovePerms(kvp.Key.ToString());
                        //kvp.Value.available_points = config.general_settings.points_per_level * kvp.Value.current_level;
                        Puts($"Finished respeccing data for {ID}");
                        return;
                    }
                }
            }
            
        }

        [ConsoleCommand("strespecallplayers")]
        void ResetSkillsCMD(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, "skilltree.admin")) return;

            ResetSkills(player);
            arg.ReplyWith("Respecced all players.");
        }

        void ResetSkills(BasePlayer player = null)
        {                        
            if (pcdData.pEntity.Count == 0)
            {
                if (player != null) PrintToChat(player, lang.GetMessage("NoPlayersSetup", this, player.UserIDString));
                return;
            }
            if (BasePlayer.activePlayerList != null && BasePlayer.activePlayerList.Count > 0)
            {
                foreach (var p in BasePlayer.activePlayerList)
                {
                    DoClear(p);
                    LoggingOff(p);
                }
            }
            buffDetails.Clear();
            TreeData.Clear();
            foreach (KeyValuePair<ulong, PlayerInfo> kvp in pcdData.pEntity)
            {
                int pointCount = 0;
                foreach (var buff in kvp.Value.buff_values)
                {
                    pointCount += buff.Value;
                }
                kvp.Value.available_points += pointCount;
                if (config.general_settings.points_per_level * kvp.Value.current_level > kvp.Value.available_points) kvp.Value.available_points = config.general_settings.points_per_level * kvp.Value.current_level;
                kvp.Value.buff_values.Clear();
                RemovePerms(kvp.Key.ToString());
                //kvp.Value.available_points = config.general_settings.points_per_level * kvp.Value.current_level;
            }

            if (BasePlayer.activePlayerList != null && BasePlayer.activePlayerList.Count > 0)
            {
                foreach (var p in BasePlayer.activePlayerList)
                {
                    HandleNewConnection(p);
                    PrintToChat(p, lang.GetMessage("PointsRefunded", this, p.UserIDString));
                }
            }
            if (player != null) PrintToChat(player, lang.GetMessage("PointsRefundedAll", this, player.UserIDString));
            SaveData();
            LoadBuffs();
        }

        #endregion

        #region API

        object QuickSortExcluded(BasePlayer player, BaseEntity entity)
        {
            if (entity!= null && entity.net != null && containers.ContainsKey(entity.net.ID)) return true;
            return null;
        }

        void OnMealConsumed(BasePlayer player, Item item, int buff_duration)
        {
            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.ContainsKey(Buff.Rationer) && RollSuccessful(bd.buff_values[Buff.Rationer]))
            {
                var refunded_item = ItemManager.CreateByName(item.info.shortname, 1, item.skin);
                if (item.name != null) refunded_item.name = item.name;
                GiveItem(player, refunded_item);
                if (NotificationsOn(player)) PrintToChat(player, string.Format(lang.GetMessage("Rationed", this, player.UserIDString), item.name ?? item.info.displayName.english));
            }
        }

        object RecipeCanModifyHorse(RidableHorse horse)
        {
            if (HorseStats.ContainsKey(horse.net.ID)) return true;
            else return null;
        }

        object ELCanModifyHorse(RidableHorse horse, float value)
        {
            if (horse == null) return null;
            var driver = horse.GetDriver();
            if (driver == null) return null;
            BuffDetails bd;
            if (!buffDetails.TryGetValue(driver.userID, out bd))
            {
                //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {driver.displayName}[{driver.UserIDString}] - ELCanModifyHorse. [Online = {driver.IsConnected}]", this, true);
                if (driver.IsConnected) PrintToChat(driver, lang.GetMessage("FailReload", this, driver.UserIDString));
                return null;
            }
            if (!bd.buff_values.ContainsKey(Buff.Riding_Speed)) return null;
            else if (bd.buff_values[Buff.Riding_Speed] > value) return true;
            RestoreHorseStats(horse);
            return null;
        }

        void OnBotReSpawnNPCKilled(ScientistNPC npc, string profile, string group, HitInfo info)
        {
            if (npc == null || string.IsNullOrEmpty(profile) || info == null || info.InitiatorPlayer == null || info.InitiatorPlayer.IsNpc || !info.InitiatorPlayer.userID.IsSteamId()) return;
            if (!config.misc_settings.botrespawn_profiles.ContainsKey(profile))
            {
                config.misc_settings.botrespawn_profiles.Add(profile, config.xp_settings.xp_sources.default_botrespawn);
                SaveConfig();
            }
            double xp;
            if (config.misc_settings.botrespawn_profiles.TryGetValue(profile, out xp))
            {
                AwardXP(info.InitiatorPlayer, xp, npc ?? null);
            }                
        }

        void HGWinner(BasePlayer player)
        {
            AwardXP(player, config.xp_settings.xp_sources.Win_HungerGames);
        }

        void SAWinner(BasePlayer player)
        {
            AwardXP(player, config.xp_settings.xp_sources.Win_ScubaArena);
        }

        void SKWinner(BasePlayer player)
        {
            AwardXP(player, config.xp_settings.xp_sources.Win_Skirmish);
        }

        void SKWinners(List<BasePlayer> players)
        {
            if (players == null || players.Count == 0) return;
            foreach (var player in players)
            {
                AwardXP(player, config.xp_settings.xp_sources.Win_Skirmish);
            }
        }

        void OnUseNPC(BasePlayer npc, BasePlayer player)
        {
            if (npc.displayName.Equals(config.misc_settings.npc_name, StringComparison.OrdinalIgnoreCase)) SendSkillTreeMenu(player);
        }

        private object OnBetterChat(Dictionary<string, object> data)
        {
            if (string.IsNullOrEmpty(config.betterchat_settings.better_title_format)) return null;
            var player = (IPlayer)data["Player"];
            if (permission.UserHasPermission(player.Id, "skilltree.notitles")) return null;
            var id = Convert.ToUInt64(player.Id);
            PlayerInfo playerData;
            if (!pcdData.pEntity.TryGetValue(id, out playerData))
            {
                SetupPlayer(id);
                playerData = pcdData.pEntity[id];
            }
            if (!playerData.better_chat_enabled) return null;
            var col = config.betterchat_settings.better_title_default_col;
            if (config.general_settings.max_player_level > 0 && playerData.current_level >= config.general_settings.max_player_level) col = config.betterchat_settings.better_title_max_col;
            var title = string.Format(config.betterchat_settings.better_title_format, col, config.general_settings.max_player_level > 0 && playerData.current_level > config.general_settings.max_player_level ? config.general_settings.max_player_level : playerData.current_level);

            var titles = (List<string>)data["Titles"];
            titles.Add(title);
            data["Titles"] = titles;
            return data;
        }

        void OnMealCrafed(BasePlayer player, string name, Dictionary<string, int> ingredients_list, bool isIngredient)
        {
            if ((!config.xp_settings.cooking_award_xp_ingredients && isIngredient) || config.xp_settings.cooking_black_list.Contains(name)) return;
            var ingredient_count = 0;
            foreach (var ingredient in ingredients_list)
            {                
                ingredient_count += ingredient.Value;
            }
            AwardXP(player, ingredient_count * config.xp_settings.xp_sources.CookingMealXP);
        }

        [HookMethod("ST_GetPlayerLevel")]
        public string[] ST_GetPlayerLevel(BasePlayer player)
        {
            PlayerInfo playerData;
            if (!pcdData.pEntity.TryGetValue(player.userID, out playerData)) return new string[] { "0", "0" };
            var level = playerData.current_level;
            if (config.general_settings.max_player_level > 0 && playerData.current_level > config.general_settings.max_player_level) level = config.general_settings.max_player_level;
            return new string[] { level.ToString(), playerData.xp.ToString() };

        }

        private void OnHarborEventWinner(ulong winnerId) => AwardEventWinnerXP(winnerId, config.xp_settings.xp_sources.Harbor_Event_Winner);
        private void OnJunkyardEventWinner(ulong winnerId) => AwardEventWinnerXP(winnerId, config.xp_settings.xp_sources.Junkyard_Event_Winner);
        private void OnSatDishEventWinner(ulong winnerId) => AwardEventWinnerXP(winnerId, config.xp_settings.xp_sources.Satellite_Event_Winner);
        private void OnWaterEventWinner(ulong winnerId) => AwardEventWinnerXP(winnerId, config.xp_settings.xp_sources.Water_Event_Winner);
        private void OnAirEventWinner(ulong winnerId) => AwardEventWinnerXP(winnerId, config.xp_settings.xp_sources.Air_Event_Winner);
        private void OnPowerPlantEventWinner(ulong winnerId) => AwardEventWinnerXP(winnerId, config.xp_settings.xp_sources.PowerPlant_Event_Winner);
        private void OnArmoredTrainEventWin(ulong winnerID) => AwardEventWinnerXP(winnerID, config.xp_settings.xp_sources.Armored_Train_Winner);
        private void OnConvoyEventWin(ulong userId) => AwardEventWinnerXP(userId, config.xp_settings.xp_sources.Convoy_Winner);
        private void OnSurvivalArenaWin(BasePlayer player) => AwardEventWinnerXP(player.userID, config.xp_settings.xp_sources.SurvivalArena_Winner);
        private void OnBossKilled(ScientistNPC boss, BasePlayer attacker) => AwardXP(attacker, config.xp_settings.xp_sources.boss_monster, boss);

        void AwardEventWinnerXP(ulong winnerID, double xp)
        {
            var player = BasePlayer.activePlayerList.FirstOrDefault(x => x.userID == winnerID);
            if (player != null) AwardXP(player, xp);
        }

        private void OnRaidableBaseCompleted(Vector3 Location, int mode, bool allowPVP, string id, float spawnTime, float despawnTime, float loadTime, ulong ownerid, BasePlayer owner, List<BasePlayer> raiders)
        {
            double xp;
            switch (mode)
            {
                case 0: 
                    xp = config.xp_settings.xp_sources.RaidableBaseCompletion_Easy;
                    break;

                case 1:
                    xp = config.xp_settings.xp_sources.RaidableBaseCompletion_Medium;
                    break;

                case 2:
                    xp = config.xp_settings.xp_sources.RaidableBaseCompletion_Hard;
                    break;

                case 3:
                    xp = config.xp_settings.xp_sources.RaidableBaseCompletion_Expert;
                    break;

                case 4:
                    xp = config.xp_settings.xp_sources.RaidableBaseCompletion_Nightmare;
                    break;

                default:
                    xp = 0;
                    break;
            }
            if (raiders != null)
            {
                foreach (var player in raiders)
                {
                    AwardXP(player, xp);
                }
            }
        }

        [HookMethod("GetExcessXP")]
        private double GetExcessXP(BasePlayer player)
        {
            PlayerInfo pi;
            if (!pcdData.pEntity.TryGetValue(player.userID, out pi))
            {
                //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {player.displayName}[{player.UserIDString}] - GetExcessXP. [Online = {player.IsConnected}]", this, true);
                if (player.IsConnected) PrintToChat(player, lang.GetMessage("FailReload", this, player.UserIDString));
                return 0;
            }
            return pi.xp - config.level.GetLevelStartXP(pi.current_level);
        }

        [HookMethod("RemoveXP")]
        private void RemoveXP(BasePlayer player, double value)
        {
            PlayerInfo pi;
            if (!pcdData.pEntity.TryGetValue(player.userID, out pi))
            {
                //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {player.displayName}[{player.UserIDString}] - RemoveXP. [Online = {player.IsConnected}]", this, true);
                if (player.IsConnected) PrintToChat(player, lang.GetMessage("FailReload", this, player.UserIDString));
                return;
            }

            var level_start_xp = config.level.GetLevelStartXP(pi.current_level);
            if ((pi.xp - value) - level_start_xp < 0.1) pi.xp = level_start_xp + 0.1;
            else pi.xp -= value;

            CheckLevel(player);
            UpdateXP(player, pi);
        }

        [HookMethod("STGetHorseStats")]
        private object STGetHorseStats(BasePlayer player, uint id)
        {
            HorseInfo stats;
            if (HorseStats.TryGetValue(id, out stats)) return new object[] { stats.horse, stats.current_maxSpeed, stats.current_runSpeed, stats.current_trotSpeed, stats.current_turnSpeed, stats.current_walkSpeed };
            else return null;
        }

        // Do a check when the player mounts the horse, see if the buff is higher than the horses modified value, and if so, modify it with the new value. When getting off the horse, if Cooking or SkillTree still have the horse stored, restore horse stats to their value and let them .

        void OpenExtraPocketsPouch(BasePlayer player)
        {
            OpenBag(player);
        }

        #endregion

        #region XP HUD        

        void UpdateXP(BasePlayer player, PlayerInfo playerData = null)
        {
            if (!permission.UserHasPermission(player.UserIDString, "skilltree.xp")) return;
            if (playerData == null)
            {
                if (!pcdData.pEntity.TryGetValue(player.userID, out playerData))
                {
                    SetupPlayer(player.userID);
                    playerData = pcdData.pEntity[player.userID];
                }
            }

            double pump_length = playerData.xp_hud_pos.max_x - playerData.xp_hud_pos.min_x;

            var cap = config.level.GetLevelStartXP(playerData.current_level + 1);

            var xpString = string.Format(lang.GetMessage("PumpBarXPText", this, player.UserIDString), Math.Round(playerData.xp, config.xp_settings.xp_rounding), Math.Round(cap, config.xp_settings.xp_rounding));
            var container = new CuiElementContainer();
            var LevelStartXP = config.level.GetLevelStartXP(playerData.current_level);
            var pump_value = (((playerData.xp - LevelStartXP) / (cap - LevelStartXP)) * pump_length) - 2.001;
            if (pump_value > pump_length) pump_value = pump_length;
            if (playerData.xp_hud_pos.min_x == 0 && playerData.xp_hud_pos.min_y == 0 && playerData.xp_hud_pos.max_x == 0 && playerData.xp_hud_pos.max_y == 0) playerData.xp_hud_pos = config.general_settings.pump_bar_settings.offset_default;
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = $"0.4245283 0.4245283 0.4245283 0.5019608" },
                RectTransform = { AnchorMin = config.general_settings.pump_bar_settings.anchor_default.anchor_min, AnchorMax = config.general_settings.pump_bar_settings.anchor_default.anchor_max, OffsetMin = $"{playerData.xp_hud_pos.min_x} {playerData.xp_hud_pos.min_y}", OffsetMax = $"{playerData.xp_hud_pos.max_x} {playerData.xp_hud_pos.max_y}" }
            }, "Hud", "SkillTreeXPBar");

            //130.001 - default value
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = config.general_settings.pump_bar_settings.pump_bar_colour },
                RectTransform = { AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "2.001 -9", OffsetMax = $"{pump_value} 9" }
            }, "SkillTreeXPBar", "SkillTreeXPBarPump");

            container.Add(new CuiElement
            {
                Name = "SkillTreeXPBarCounter",
                Parent = "SkillTreeXPBar",
                Components = {
                    new CuiTextComponent { Text = xpString, Font = config.general_settings.pump_bar_settings.pump_bar_font, FontSize = config.general_settings.pump_bar_settings.pump_bar_font_size, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7843137" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-32.125 -11", OffsetMax = "61.858 11" }
                }
            });
            var level = config.general_settings.max_player_level > 0 && playerData.current_level > config.general_settings.max_player_level ? config.general_settings.max_player_level : playerData.current_level;
            container.Add(new CuiElement
            {
                Name = "SkillTreeXPBarTitle",
                Parent = "SkillTreeXPBar",
                Components = {
                    new CuiTextComponent { Text = string.Format(lang.GetMessage("PumpBarLevelText", this, player.UserIDString), level), Font = config.general_settings.pump_bar_settings.pump_bar_font, FontSize = config.general_settings.pump_bar_settings.pump_bar_font_size + 2, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.7843137" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-66.213 -11", OffsetMax = "-32.125 11" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = "openskilltreemenufrompumpbar" },
                Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-66.209 -11", OffsetMax = "66.211 11" }
            }, "SkillTreeXPBar", "button");

            CuiHelper.DestroyUi(player, "SkillTreeXPBar");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("openskilltreemenufrompumpbar")]
        void OpenSkillTreeMenuFromPumpBar(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            SendMenuCMD(player);
        }


        #endregion

        #region XPBar Mover

        void SendXPBarMoverMenu(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0.5377358 0.5377358 0.5377358 0.5019608" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50.003 -50", OffsetMax = "49.997 50" }
            }, "ContentUI", "ui_mover");

            if (config.general_settings.image_cache_source.Equals("url", StringComparison.OrdinalIgnoreCase))
            {
                container.Add(new CuiElement
                {
                    Name = "ui_mover_up_double_img",
                    Parent = "ui_mover",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = (string)ImageLibrary?.Call("GetImage", "arrow_up_double") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 34", OffsetMax = "8 50" }
                }
                });
                container.Add(new CuiButton
                {
                    Button = { Color = "1 0.6885808 0 0.2745098", Command = $"movexpbarui u2" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 -8", OffsetMax = "8 8" }
                }, "ui_mover_up_double_img", "ui_mover_up_double_button");

                container.Add(new CuiElement
                {
                    Name = "ui_mover_up_single_img",
                    Parent = "ui_mover",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = (string)ImageLibrary?.Call("GetImage", "arrow_up_single") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 13", OffsetMax = "8 29" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 0.6885808 0 0.2745098", Command = $"movexpbarui u1" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 -8", OffsetMax = "8 8" }
                }, "ui_mover_up_single_img", "ui_mover_up_single_button");

                container.Add(new CuiElement
                {
                    Name = "ui_mover_down_double_img",
                    Parent = "ui_mover",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = (string)ImageLibrary?.Call("GetImage", "arrow_down_double") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 -50", OffsetMax = "8 -34" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 0.6885808 0 0.2745098", Command = $"movexpbarui d2" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 -8", OffsetMax = "8 8" }
                }, "ui_mover_down_double_img", "ui_mover_down_double_button");

                container.Add(new CuiElement
                {
                    Name = "ui_mover_down_single_img",
                    Parent = "ui_mover",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = (string)ImageLibrary?.Call("GetImage", "arrow_down_single") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 -29", OffsetMax = "8 -13" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 0.6885808 0 0.2745098", Command = $"movexpbarui d1" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 -8", OffsetMax = "8 8" }
                }, "ui_mover_down_single_img", "ui_mover_down_single_button");

                container.Add(new CuiElement
                {
                    Name = "ui_mover_left_double_img",
                    Parent = "ui_mover",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = (string)ImageLibrary?.Call("GetImage", "arrow_left_double") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50 -8", OffsetMax = "-34 8" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 0.6885808 0 0.2745098", Command = $"movexpbarui l2" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 -8", OffsetMax = "8 8" }
                }, "ui_mover_left_double_img", "ui_mover_left_double_button");

                container.Add(new CuiElement
                {
                    Name = "ui_mover_left_single_img",
                    Parent = "ui_mover",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = (string)ImageLibrary?.Call("GetImage", "arrow_left_single") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-29 -8", OffsetMax = "-13 8" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 0.6885808 0 0.2745098", Command = $"movexpbarui l1" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 -8", OffsetMax = "8 8" }
                }, "ui_mover_left_single_img", "ui_mover_left_single_button");

                container.Add(new CuiElement
                {
                    Name = "ui_mover_right_double_img",
                    Parent = "ui_mover",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = (string)ImageLibrary?.Call("GetImage", "arrow_right_double") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "34 -8", OffsetMax = "50 8" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 0.6885808 0 0.2745098", Command = $"movexpbarui r2" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 -8", OffsetMax = "8 8" }
                }, "ui_mover_right_double_img", "ui_mover_right_double_button");

                container.Add(new CuiElement
                {
                    Name = "ui_mover_right_single_img",
                    Parent = "ui_mover",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = (string)ImageLibrary?.Call("GetImage", "arrow_right_single") },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "13 -8", OffsetMax = "29 8" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 0.6885808 0 0.2745098", Command = $"movexpbarui r1" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 -8", OffsetMax = "8 8" }
                }, "ui_mover_right_single_img", "ui_mover_right_single_button");
            }                
            else
            {
                container.Add(new CuiElement
                {
                    Name = "ui_mover_up_double_img",
                    Parent = "ui_mover",
                    Components = {
                    new CuiImageComponent { Color = "1 1 1 1", ItemId = 1751045826, SkinId = ArrowSkins["arrow_up_double"] },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 34", OffsetMax = "8 50" }
                }
                });
                container.Add(new CuiButton
                {
                    Button = { Color = "1 0.6885808 0 0.2745098", Command = $"movexpbarui u2" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 -8", OffsetMax = "8 8" }
                }, "ui_mover_up_double_img", "ui_mover_up_double_button");

                container.Add(new CuiElement
                {
                    Name = "ui_mover_up_single_img",
                    Parent = "ui_mover",
                    Components = {
                    new CuiImageComponent { Color = "1 1 1 1", ItemId = 1751045826, SkinId = ArrowSkins["arrow_up_single"] },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 13", OffsetMax = "8 29" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 0.6885808 0 0.2745098", Command = $"movexpbarui u1" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 -8", OffsetMax = "8 8" }
                }, "ui_mover_up_single_img", "ui_mover_up_single_button");

                container.Add(new CuiElement
                {
                    Name = "ui_mover_down_double_img",
                    Parent = "ui_mover",
                    Components = {
                    new CuiImageComponent { Color = "1 1 1 1", ItemId = 1751045826, SkinId = ArrowSkins["arrow_down_double"] },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 -50", OffsetMax = "8 -34" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 0.6885808 0 0.2745098", Command = $"movexpbarui d2" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 -8", OffsetMax = "8 8" }
                }, "ui_mover_down_double_img", "ui_mover_down_double_button");

                container.Add(new CuiElement
                {
                    Name = "ui_mover_down_single_img",
                    Parent = "ui_mover",
                    Components = {
                    new CuiImageComponent { Color = "1 1 1 1", ItemId = 1751045826, SkinId = ArrowSkins["arrow_down_single"] },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 -29", OffsetMax = "8 -13" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 0.6885808 0 0.2745098", Command = $"movexpbarui d1" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 -8", OffsetMax = "8 8" }
                }, "ui_mover_down_single_img", "ui_mover_down_single_button");

                container.Add(new CuiElement
                {
                    Name = "ui_mover_left_double_img",
                    Parent = "ui_mover",
                    Components = {
                    new CuiImageComponent { Color = "1 1 1 1", ItemId = 1751045826, SkinId = ArrowSkins["arrow_left_double"] },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50 -8", OffsetMax = "-34 8" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 0.6885808 0 0.2745098", Command = $"movexpbarui l2" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 -8", OffsetMax = "8 8" }
                }, "ui_mover_left_double_img", "ui_mover_left_double_button");

                container.Add(new CuiElement
                {
                    Name = "ui_mover_left_single_img",
                    Parent = "ui_mover",
                    Components = {
                    new CuiImageComponent { Color = "1 1 1 1", ItemId = 1751045826, SkinId = ArrowSkins["arrow_left_single"] },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-29 -8", OffsetMax = "-13 8" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 0.6885808 0 0.2745098", Command = $"movexpbarui l1" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 -8", OffsetMax = "8 8" }
                }, "ui_mover_left_single_img", "ui_mover_left_single_button");

                container.Add(new CuiElement
                {
                    Name = "ui_mover_right_double_img",
                    Parent = "ui_mover",
                    Components = {
                    new CuiImageComponent { Color = "1 1 1 1", ItemId = 1751045826, SkinId = ArrowSkins["arrow_right_double"] },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "34 -8", OffsetMax = "50 8" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 0.6885808 0 0.2745098", Command = $"movexpbarui r2" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 -8", OffsetMax = "8 8" }
                }, "ui_mover_right_double_img", "ui_mover_right_double_button");

                container.Add(new CuiElement
                {
                    Name = "ui_mover_right_single_img",
                    Parent = "ui_mover",
                    Components = {
                    new CuiImageComponent { Color = "1 1 1 1", ItemId = 1751045826, SkinId = ArrowSkins["arrow_right_single"] },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "13 -8", OffsetMax = "29 8" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 0.6885808 0 0.2745098", Command = $"movexpbarui r1" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-8 -8", OffsetMax = "8 8" }
                }, "ui_mover_right_single_img", "ui_mover_right_single_button");
            }
            

            container.Add(new CuiButton
            {
                Button = { Color = "0.1860092 0.1868992 0.1886792 1", Command = "closeuimover" },
                Text = { Text = "X", Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "34 34", OffsetMax = "50 50" }
            }, "ui_mover", "ui_mover_close");

            CuiHelper.DestroyUi(player, "ui_mover");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("closeuimover")]
        void CloseUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            CuiHelper.DestroyUi(player, "ui_mover");
        }

        [ConsoleCommand("movexpbarui")]
        void MoveXPBar(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            PlayerInfo playerData;

            if (!pcdData.pEntity.TryGetValue(player.userID, out playerData))
            {
                SetupPlayer(player.userID);
                playerData = pcdData.pEntity[player.userID];
            }

            switch (arg.Args[0])
            {
                case "u1":
                    playerData.xp_hud_pos.min_y += 5;
                    playerData.xp_hud_pos.max_y += 5;
                    break;
                case "u2":
                    playerData.xp_hud_pos.min_y += 20;
                    playerData.xp_hud_pos.max_y += 20;
                    break;
                case "d1":
                    playerData.xp_hud_pos.min_y -= 5;
                    playerData.xp_hud_pos.max_y -= 5;
                    break;
                case "d2":
                    playerData.xp_hud_pos.min_y -= 20;
                    playerData.xp_hud_pos.max_y -= 20;
                    break;
                case "l1":
                    playerData.xp_hud_pos.min_x -= 5;
                    playerData.xp_hud_pos.max_x -= 5;
                    break;
                case "l2":
                    playerData.xp_hud_pos.min_x -= 20;
                    playerData.xp_hud_pos.max_x -= 20;
                    break;
                case "r1":
                    playerData.xp_hud_pos.min_x += 5;
                    playerData.xp_hud_pos.max_x += 5;
                    break;
                case "r2":
                    playerData.xp_hud_pos.min_x += 20;
                    playerData.xp_hud_pos.max_x += 20;
                    break;
            }
            UpdateXP(player, playerData);
        }

        #endregion

        #region Furnace speed

        Dictionary<BaseOven, float> ovens = new Dictionary<BaseOven, float>();

        void OnFuelConsume(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            float modifier;
            if (!ovens.TryGetValue(oven, out modifier) || !RollSuccessful(modifier)) return;
            List<Item> remove_items = new List<Item>();
            foreach (var item in oven.inventory.itemList.ToList())
            {
                var itemModCookable = item.info.GetComponent<ItemModCookable>();
                if (itemModCookable?.becomeOnCooked == null || item.temperature < itemModCookable.lowTemp || item.temperature > itemModCookable.highTemp || itemModCookable.cookTime < 0) continue;
                var itemToGive = ItemManager.Create(itemModCookable.becomeOnCooked, itemModCookable.amountOfBecome);
                if (!itemToGive.MoveToContainer(oven.inventory))
                    itemToGive.Drop(oven.inventory.dropPosition, oven.inventory.dropVelocity);
                if (item.amount == 1) item.Remove();
                else item.SplitItem(1).Remove();
            }
            foreach (var item in remove_items.ToList())
            {
                item.Remove();
            }
        }

        void OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (oven.temperature != BaseOven.TemperatureType.Smelting) return;
            // Checks if the oven is on when the toggle occurs, and if it is, we exit because its being turned off.
            if (oven.IsOn())
            {
                if (ovens.ContainsKey(oven)) ovens.Remove(oven);
                return;
            }
            // See if the player has the buff assigned.
            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.ContainsKey(Buff.Smelt_Speed))
            {
                if (oven.inventory.itemList == null || oven.inventory.itemList.Count == 0) return;
                ovens.Remove(oven);
                ovens.Add(oven, bd.buff_values[Buff.Smelt_Speed]);
            }
        }

        #endregion

        #region Subscriptions

        public Dictionary<string, Subscription> subscriptions = new Dictionary<string, Subscription>()
        {
            [nameof(OnEntityTakeDamage)] = new Subscription(false, new List<Buff>() { Buff.Animal_Damage_Resist, Buff.Barrel_Smasher, Buff.Fall_Damage_Reduction, Buff.Fire_Damage_Reduction, Buff.Melee_Resist, Buff.No_Cold_Damage, Buff.PVP_Critical, Buff.PVP_Damage, Buff.PVP_Shield, Buff.Radiation_Reduction, Buff.Loot_Pickup, Buff.Human_NPC_Damage, Buff.Human_NPC_Defence, Buff.Animal_NPC_Damage, Buff.Vehicle_Ultimate, Buff.Combat_Ultimate, Buff.Scavengers_Ultimate, Buff.Combat_Ultimate, Buff.SharkResistance, Buff.UnderwaterDamageBonus, Buff.Skinning_Ultimate }),
            [nameof(OnItemUse)] = new Subscription(false, new List<Buff>() { Buff.Rationer }),
            [nameof(OnPlayerRevive)] = new Subscription(false, new List<Buff>() { Buff.Reviver }),
            [nameof(OnPlayerHealthChange)] = new Subscription(false, new List<Buff>() { Buff.Double_Bandage_Heal }),
            [nameof(OnLoseCondition)] = new Subscription(false, new List<Buff>() { Buff.Woodcutting_Tool_Durability, Buff.Mining_Tool_Durability, Buff.Skinning_Tool_Durability, Buff.Primitive_Expert, Buff.Durability }),
            [nameof(OnWeaponFired)] = new Subscription(false, new List<Buff>() { Buff.Free_Bullet_Chance }),
            [nameof(OnRecyclerToggle)] = new Subscription(false, new List<Buff>() { Buff.Recycler_Speed }),
            [nameof(OnPlayerAddModifiers)] = new Subscription(false, new List<Buff>() { Buff.Extra_Food_Water, Buff.Iron_Stomach }),
            [nameof(OnPlayerWound)] = new Subscription(false, new List<Buff>() { Buff.Wounded_Resist }),
            [nameof(OnEntityMounted)] = new Subscription(false, new List<Buff>() { Buff.Riding_Speed, Buff.Heli_Fuel_Rate, Buff.Boat_Fuel_Rate }),
            [nameof(OnEntityDismounted)] = new Subscription(false, new List<Buff>() { Buff.Boat_Fuel_Rate, Buff.Heli_Fuel_Rate, Buff.Riding_Speed, Buff.Boat_Speed }),
            [nameof(OnHammerHit)] = new Subscription(false, new List<Buff>() { Buff.Vehicle_Mechanic }),
            [nameof(OnPayForUpgrade)] = new Subscription(false, new List<Buff>() { Buff.Upgrade_Refund }),
            [nameof(OnPlayerInput)] = new Subscription(false, new List<Buff>() { Buff.Boat_Speed }),
            [nameof(OnResearchCostDetermine)] = new Subscription(false, new List<Buff>() { Buff.Research_Refund }),
            [nameof(CanLootEntity)] = new Subscription(false, new List<Buff>() { Buff.Component_Chest, Buff.Electronic_Chest, Buff.Extra_Scrap_Crate, Buff.DeepSeaLooter }),
            [nameof(OnPlayerRespawned)] = new Subscription(false, new List<Buff>() { Buff.Medical_Ultimate }),
            [nameof(OnItemRepair)] = new Subscription(false, new List<Buff>() { Buff.MaxRepair })
        };
        

        public class Subscription
        {
            public bool isSubscribed;
            public List<Buff> buffs;
            public List<ulong> subscribers = new List<ulong>();
            public Subscription(bool isSubscribed, List<Buff> buffs)
            {
                this.isSubscribed = isSubscribed;
                this.buffs = buffs;
            }
            public void Subscribed()
            {
                this.isSubscribed = true;
                if (this.subscribers == null) this.subscribers = new List<ulong>();
            }
            public void Unsubscribed()
            {
                this.isSubscribed = false;
                this.subscribers.Clear();
            }
            public bool Required()
            {
                if (this.subscribers != null && this.subscribers.Count > 0) return true;
                return false;
            }
            public void AddPlayer(ulong id)
            {
                if (this.subscribers == null) this.subscribers = new List<ulong>();
                if (!this.subscribers.Contains(id)) this.subscribers.Add(id);
            }
            public void RemovePlayer(ulong id)
            {
                if (this.subscribers != null)
                {
                    this.subscribers.Remove(id);
                }
            }
        }

        void RemoveFromAllBuffs(ulong id)
        {
            foreach (var sub in subscriptions)
            {
                sub.Value.subscribers.Remove(id);
                if (sub.Value.isSubscribed && !sub.Value.Required())
                {
                    sub.Value.Unsubscribed();
                    Unsubscribe(sub.Key);
                }
            }
        }

        void AddBuffs(ulong id, Buff buff)
        {
            foreach (var sub in subscriptions)
            {
                if (!sub.Value.buffs.Contains(buff)) continue;
                sub.Value.AddPlayer(id);
                if (!sub.Value.isSubscribed)
                {
                    sub.Value.Subscribed();
                    Subscribe(sub.Key);
                }
            }
        }

        void LoadBuffs()
        {
            if (BasePlayer.activePlayerList == null || BasePlayer.activePlayerList.Count == 0 || buffDetails == null || buffDetails.Count == 0)
            {
                foreach (var sub in subscriptions)
                {
                    Unsubscribe(sub.Key);
                    sub.Value.Unsubscribed();
                }
            }
            else
            {
                foreach (var sub in subscriptions)
                {
                    foreach (var player in buffDetails)
                    {
                        if (player.Value.buff_values == null || player.Value.buff_values.Count == 0) continue;
                        foreach (var buff in player.Value.buff_values)
                        {
                            if (sub.Value.buffs.Contains(buff.Key))
                            {
                                sub.Value.AddPlayer(player.Key);
                                sub.Value.Subscribed();
                            }
                        }
                    }
                    if (!sub.Value.Required())
                    {
                        sub.Value.Unsubscribed();
                        Unsubscribe(sub.Key);
                    }
                }
            }
        }

        #endregion

        #region Player Menu

        private void SkillTree_PlayerMenu(BasePlayer player)
        {
            PlayerInfo pi;
            if (!pcdData.pEntity.TryGetValue(player.userID, out pi)) return;

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0.9803922" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0.025 -0.331", OffsetMax = "0.325 0.339" }
            }, "ContentUI", "SkillTree_PlayerMenu");

            container.Add(new CuiElement
            {
                Name = "SkillTree_PlayerMenu_Title",
                Parent = "SkillTree_PlayerMenu",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("UIPlayerSettings", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 26, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-495.24 190.4", OffsetMax = "-180.18 250.4" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1132075 0.1073335 0.1073335 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-495.24 132.4", OffsetMax = "-180.18 190.4" }
            }, "SkillTree_PlayerMenu", "SkillTree_PlayerMenu_tgl_pnl_bk_1");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.2264151 0.2264151 0.2264151 0.9607843" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-155.53 -27", OffsetMax = "155.53 27" }
            }, "SkillTree_PlayerMenu_tgl_pnl_bk_1", "SkillTree_PlayerMenu_tgl_pnl_ft_1");

            container.Add(new CuiElement
            {
                Name = "SkillTree_PlayerMenu_tgl_des_1",
                Parent = "SkillTree_PlayerMenu_tgl_pnl_bk_1",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("UIToggleXP", this, player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-147 -26", OffsetMax = "52.24 26" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1132075 0.1073335 0.1073335 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "66.53 -24", OffsetMax = "152.53 24" }
            }, "SkillTree_PlayerMenu_tgl_pnl_bk_1", "SkillTree_PlayerMenu_tgl_bttn_pnl_1");

            var textCol = "0.0480598 0.6792453 0.1672014 1";
            if (!pi.xp_drops) textCol = "0.5943396 0.131764 0.1842591 1";

            container.Add(new CuiButton
            {
                Button = { Color = "0.1607843 0.1607843 0.1607843 1", Command = $"dotogglexpdrops" },
                Text = { Text = pi.xp_drops ? lang.GetMessage("ON", this, player.UserIDString) : lang.GetMessage("OFF", this, player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = textCol },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "68.53 -22", OffsetMax = "150.53 22" }
            }, "SkillTree_PlayerMenu_tgl_pnl_bk_1", "SkillTree_PlayerMenu_tgl_bttn_1");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1132075 0.1073335 0.1073335 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-495.24 54.4", OffsetMax = "-180.18 112.4" }
            }, "SkillTree_PlayerMenu", "SkillTree_PlayerMenu_tgl_pnl_bk_2");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.2264151 0.2264151 0.2264151 0.9607843" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-155.53 -27", OffsetMax = "155.53 27" }
            }, "SkillTree_PlayerMenu_tgl_pnl_bk_2", "SkillTree_PlayerMenu_tgl_pnl_ft_2");

            container.Add(new CuiElement
            {
                Name = "SkillTree_PlayerMenu_tgl_des_2",
                Parent = "SkillTree_PlayerMenu_tgl_pnl_bk_2",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("UIToggleXPBar", this, player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-147 -26", OffsetMax = "52.24 26" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1132075 0.1073335 0.1073335 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "66.53 -24", OffsetMax = "152.53 24" }
            }, "SkillTree_PlayerMenu_tgl_pnl_bk_2", "SkillTree_PlayerMenu_tgl_bttn_pnl_2");

            textCol = "0.0480598 0.6792453 0.1672014 1";
            if (!pi.xp_hud) textCol = "0.5943396 0.131764 0.1842591 1";

            container.Add(new CuiButton
            {
                Button = { Color = "0.1607843 0.1607843 0.1607843 1", Command = "dotogglexphud" },
                Text = { Text = pi.xp_hud ? lang.GetMessage("ON", this, player.UserIDString) : lang.GetMessage("OFF", this, player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = textCol },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "68.53 -22", OffsetMax = "150.53 22" }
            }, "SkillTree_PlayerMenu_tgl_pnl_bk_2", "SkillTree_PlayerMenu_tgl_bttn_2");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1132075 0.1073335 0.1073335 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-495.24 -23.6", OffsetMax = "-180.18 34.4" }
            }, "SkillTree_PlayerMenu", "SkillTree_PlayerMenu_tgl_pnl_bk_3");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.2264151 0.2264151 0.2264151 0.9607843" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-155.53 -27", OffsetMax = "155.53 27" }
            }, "SkillTree_PlayerMenu_tgl_pnl_bk_3", "SkillTree_PlayerMenu_tgl_pnl_ft_3");

            container.Add(new CuiElement
            {
                Name = "SkillTree_PlayerMenu_tgl_des_3",
                Parent = "SkillTree_PlayerMenu_tgl_pnl_bk_3",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("RepositionBar", this, player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-147 -26", OffsetMax = "52.24 26" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1132075 0.1073335 0.1073335 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "66.53 -24", OffsetMax = "152.53 24" }
            }, "SkillTree_PlayerMenu_tgl_pnl_bk_3", "SkillTree_PlayerMenu_tgl_bttn_pnl_3");

            container.Add(new CuiButton
            {
                Button = { Color = "0.1607843 0.1607843 0.1607843 1", Command = "strepositionhudfrommenu" },
                Text = { Text = lang.GetMessage("UIChange", this, player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 0.7984455 0.3066038 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "68.53 -22", OffsetMax = "150.53 22" }
            }, "SkillTree_PlayerMenu_tgl_pnl_bk_3", "SkillTree_PlayerMenu_tgl_bttn_3");

            textCol = "0.0480598 0.6792453 0.1672014 1";
            if (!pi.extra_pockets_button) textCol = "0.5943396 0.131764 0.1842591 1";

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1132075 0.1073335 0.1073335 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-495.24 -101.6", OffsetMax = "-180.18 -43.6" }
            }, "SkillTree_PlayerMenu", "SkillTree_PlayerMenu_tgl_pnl_bk_4");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.2264151 0.2264151 0.2264151 0.9607843" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-155.53 -27", OffsetMax = "155.53 27" }
            }, "SkillTree_PlayerMenu_tgl_pnl_bk_4", "SkillTree_PlayerMenu_tgl_pnl_ft_4");

            container.Add(new CuiElement
            {
                Name = "SkillTree_PlayerMenu_tgl_des_4",
                Parent = "SkillTree_PlayerMenu_tgl_pnl_bk_4",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("ToggleBagButton", this, player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-147 -26", OffsetMax = "52.24 26" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1132075 0.1073335 0.1073335 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "66.53 -24", OffsetMax = "152.53 24" }
            }, "SkillTree_PlayerMenu_tgl_pnl_bk_4", "SkillTree_PlayerMenu_tgl_bttn_pnl_4");

            container.Add(new CuiButton
            {
                Button = { Color = "0.1607843 0.1607843 0.1607843 1", Command = "sttoggleextrapocketsbutton" },
                Text = { Text = pi.extra_pockets_button ? lang.GetMessage("ON", this, player.UserIDString) : lang.GetMessage("OFF", this, player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = textCol },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "68.53 -22", OffsetMax = "150.53 22" }
            }, "SkillTree_PlayerMenu_tgl_pnl_bk_4", "SkillTree_PlayerMenu_tgl_bttn_4");

            textCol = "0.0480598 0.6792453 0.1672014 1";
            if (!pi.notifications) textCol = "0.5943396 0.131764 0.1842591 1";

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1132075 0.1073335 0.1073335 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-495.24 -179.6", OffsetMax = "-180.18 -121.6" }
            }, "SkillTree_PlayerMenu", "SkillTree_PlayerMenu_tgl_pnl_bk_5");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.2264151 0.2264151 0.2264151 0.9607843" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-155.53 -27", OffsetMax = "155.53 27" }
            }, "SkillTree_PlayerMenu_tgl_pnl_bk_5", "SkillTree_PlayerMenu_tgl_pnl_ft_5");

            container.Add(new CuiElement
            {
                Name = "SkillTree_PlayerMenu_tgl_des_5",
                Parent = "SkillTree_PlayerMenu_tgl_pnl_bk_5",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("ToggleNotifications", this, player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-147 -26", OffsetMax = "52.24 26" }
                }
            });

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1132075 0.1073335 0.1073335 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "66.53 -24", OffsetMax = "152.53 24" }
            }, "SkillTree_PlayerMenu_tgl_pnl_bk_5", "SkillTree_PlayerMenu_tgl_bttn_pnl_5");

            container.Add(new CuiButton
            {
                Button = { Color = "0.1607843 0.1607843 0.1607843 1", Command = "sttogglenotifications" },
                Text = { Text = pi.notifications ? lang.GetMessage("ON", this, player.UserIDString) : lang.GetMessage("OFF", this, player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = textCol },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "68.53 -22", OffsetMax = "150.53 22" }
            }, "SkillTree_PlayerMenu_tgl_pnl_bk_5", "SkillTree_PlayerMenu_tgl_bttn_5");


            container.Add(new CuiElement
            {
                Name = "SkillTree_PlayerMenu_close",
                Parent = "SkillTree_PlayerMenu",
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("UIClose", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-366.71 -243", OffsetMax = "-308.71 -211" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = $"stcloseplayersettings" },
                Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-29 -16", OffsetMax = "29 16" }
            }, "SkillTree_PlayerMenu_close", "SkillTree_PlayerMenu_close_button");

            CuiHelper.DestroyUi(player, "SkillTree_PlayerMenu");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("sttoggleextrapocketsbutton")]
        void ToggleExtraPockets(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            PlayerInfo pi;
            if (!pcdData.pEntity.TryGetValue(player.userID, out pi))
            {
                SetupPlayer(player.userID);
                pi = pcdData.pEntity[player.userID];
            }
            if (pi.extra_pockets_button)
            {
                pi.extra_pockets_button = false;
                SkillTree_PlayerMenu(player);
                CuiHelper.DestroyUi(player, "ExtraPocketsButton");
            }
            else
            {
                if (buffDetails.ContainsKey(player.userID) && buffDetails[player.userID].buff_values.ContainsKey(Buff.ExtraPockets))
                {                    
                    SendExtraPocketsButton(player);                    
                }
                pi.extra_pockets_button = true;
                SkillTree_PlayerMenu(player);
            }
        }
        
        [ConsoleCommand("sttogglenotifications")]
        void ToggleNotifications(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            ToggleNotifications(player);
            SkillTree_PlayerMenu(player);
        }

        [ConsoleCommand("dotogglexpdrops")]
        void ToggleXPDropsFromMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            ToggleXPDrops(player);
            SkillTree_PlayerMenu(player);
        }

        [ConsoleCommand("dotogglexphud")]
        void ToggleXPHudFromMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            ToggleXPHud(player);
            SkillTree_PlayerMenu(player);
        }

        [ConsoleCommand("strepositionhudfrommenu")]
        void RepositionHudFromMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            CuiHelper.DestroyUi(player, "SkillTree_PlayerMenu");
            CuiHelper.DestroyUi(player, "SkillTree");
            CuiHelper.DestroyUi(player, "SkillTreeBackPanel");
            MoveBar(player);
        }

        [ConsoleCommand("stcloseplayersettings")]
        void ClosePlayerSettings(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            CuiHelper.DestroyUi(player, "SkillTree_PlayerMenu");
        }

        #endregion

        #region Health Regen

        private static Dictionary<BasePlayer, float> RegenAmount = new Dictionary<BasePlayer, float>();

        bool HasRegen(BasePlayer player)
        {
            return player.GetComponent<Regen>() != null;
        }

        void UpdateRegen(BasePlayer player, float value)
        {
            DestroyRegen(player);
            RegenAmount.Add(player, value);
            player.gameObject.AddComponent<Regen>();
        }

        void DestroyRegen(BasePlayer player)
        {
            if (RegenAmount.ContainsKey(player)) RegenAmount.Remove(player);
            var gameObject = player.GetComponent<Regen>();
            if (gameObject != null) GameObject.DestroyImmediate(gameObject);
        }

        public static Dictionary<ulong, float> took_damage = new Dictionary<ulong, float>();

        void AddRegenDelay(BasePlayer player)
        {
            if (config.buff_settings.health_regen_combat_delay <= 0) return;
            BuffDetails bd;
            if (!buffDetails.TryGetValue(player.userID, out bd) || !bd.buff_values.ContainsKey(Buff.HealthRegen)) return;
            if (!took_damage.ContainsKey(player.userID))
            {
                took_damage.Add(player.userID, Time.time + config.buff_settings.health_regen_combat_delay);
                if (NotificationsOn(player)) PrintToChat(player, string.Format(lang.GetMessage("DisabledRegen", this, player.UserIDString), config.buff_settings.health_regen_combat_delay));
            }                
            else took_damage[player.userID] = Time.time + config.buff_settings.health_regen_combat_delay;            
        }

        public class Regen : MonoBehaviour
        {
            private BasePlayer player;
            private float regenDelay;
            private float _regenAmount;

            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                regenDelay = Time.time + 1f;
                _regenAmount = RegenAmount[player];
            }

            public void FixedUpdate()
            {
                if (player == null) return;
                if (regenDelay < Time.time)
                {
                    if (took_damage.ContainsKey(player.userID))
                    {
                        if (took_damage[player.userID] > Time.time) return;
                        else took_damage.Remove(player.userID);
                    }
                    regenDelay = Time.time + 1f;
                    DoRegen();
                }
            }

            public void DoRegen()
            {
                if (player == null || !player.IsConnected || !player.IsAlive() || player.health == player.MaxHealth()) return;
                player.Heal(_regenAmount);
            }

            private void OnDestroy()
            {
                enabled = false;
                CancelInvoke();
            }
        }

        #endregion

        #region AnimalTracker

        Dictionary<BasePlayer, float> track_delays = new Dictionary<BasePlayer, float>();

        [ChatCommand("track")]
        void TrackAnimal(BasePlayer player)
        {
            BuffDetails bd;
            if (!buffDetails.TryGetValue(player.userID, out bd))
            {
                //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {player.displayName}[{player.UserIDString}] - TrackAnimal. [Online = {player.IsConnected}]", this, true);
                if (player.IsConnected) PrintToChat(player, lang.GetMessage("FailReload", this, player.UserIDString));
                return;
            }
            if (!bd.buff_values.ContainsKey(Buff.AnimalTracker)) return;

            if (!track_delays.ContainsKey(player)) track_delays.Add(player, Time.time + config.buff_settings.track_delay);
            else if (track_delays[player] < Time.time) track_delays[player] = Time.time + config.buff_settings.track_delay;
            else
            {
                PrintToChat(player, string.Format(lang.GetMessage("TrackWait", this, player.UserIDString), Math.Round(track_delays[player] - Time.time, 2)));
                return;
            }

            List<BaseAnimalNPC> animals = Pool.GetList<BaseAnimalNPC>();
            animals.AddRange(FindEntitiesOfType<BaseAnimalNPC>(player.transform.position, 300f));
            BaseAnimalNPC animal = animals.Count > 0 ? animals.OrderBy(x => Vector3.Distance(x.transform.position, player.transform.position)).First() : null;            

            if (animal == null)
            {
                PrintToChat(player, lang.GetMessage("NoAnimals", this, player.UserIDString));
                Pool.FreeList(ref animals);
                return;
            }

            var distance = Vector3.Distance(player.transform.position, animal.transform.position);
            string text;
            if (distance < 50) text = lang.GetMessage("TrackFresh", this, player.UserIDString);
            else if (distance < 100) text = lang.GetMessage("TrackOlder", this, player.UserIDString);
            else text = lang.GetMessage("TrackOldest", this, player.UserIDString);
            var direction = player.transform.position - animal.transform.position;
            direction.Normalize();
            PrintToChat(player, string.Format(text, Direction(direction.ZX2D())));
            Pool.FreeList(ref animals);
        }

        string Direction(Vector2 dir)
        {
            if (dir.x >= -1.0 && dir.x <= -0.8 && dir.y >= -0.5 && dir.y <= 0.5) return "North";
            if (dir.x >= -1.0 && dir.x <= -0.5 && dir.y >= 0.5 && dir.y <= 1.0) return "North-West";
            if (dir.x >= -0.5 && dir.x <= 0.5 && dir.y >= 0.8 && dir.y <= 1.0) return "West";
            if (dir.x >= 0.5 && dir.x <= 1.0 && dir.y >= 0.5 && dir.y <= 1.0) return "South-West";
            if (dir.x >= -0.5 && dir.x <= 0.5 && dir.y >= -1.0 && dir.y <= -0.8) return "East";
            if (dir.x >= -1.0 && dir.x <= -0.5 && dir.y >= -1.0 && dir.y <= -0.5) return "North-East";
            if (dir.x >= 0.5 && dir.x <= 1.0 && dir.y >= -1.0 && dir.y <= -0.5) return "South-East";
            if (dir.x >= 0.8 && dir.x <= 1.0 && dir.y >= -0.5 && dir.y <= 0.5) return "South";
            return null;
        }

        #endregion

        #region Extra Pockets

        public class ItemInfo
        {
            public string shortname;
            public ulong skin;
            public int amount;
            public float condition;
            public float maxCondition;
            public int ammo;
            public string ammotype;
            public int position;
            public int frequency;
            public KeyInfo instanceData;
            public class KeyInfo
            {
                public int dataInt;
                public int blueprintTarget;
                public int blueprintAmount;
            }
            public ItemInfo[] contents;
            public List<ItemInfo> item_contents;
            public string text;
            public string name;
        }

        Dictionary<ulong, float> bagCooldown = new Dictionary<ulong, float>();

        [ChatCommand("pouch")]
        void OpenBagCMD(BasePlayer player)
        {
            OpenBag(player);
        }

        [ConsoleCommand("pouch")]
        void OpenBagConsoleCMD(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            OpenBag(player);
        }

        void OpenBag(BasePlayer player)
        {
            if (player.IsDead() || Interface.CallHook("STOnPouchOpen", player) != null) return;
            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.ContainsKey(Buff.ExtraPockets))
            {
                // Handle cooldown
                if (!bagCooldown.ContainsKey(player.userID)) bagCooldown.Add(player.userID, Time.time + config.buff_settings.bag_cooldown_time);
                else
                {
                    if (bagCooldown[player.userID] < Time.time) bagCooldown[player.userID] = Time.time + config.buff_settings.bag_cooldown_time;
                    else
                    {
                        PrintToChat(player, $"You must wait {Math.Round(bagCooldown[player.userID] - Time.time, 2)} seconds before attempting to open your bag again.");
                        return;
                    }
                }
                player.EndLooting();
                var bag = GenerateBag(player, Convert.ToInt32(bd.buff_values[Buff.ExtraPockets]));
                timer.Once(0.1f, () =>
                {
                    if (bag != null) bag.PlayerOpenLoot(player, "", false);
                    Interface.CallHook("STOnPouchOpened", player, bag);
                });
            }
            else
            {
                PrintToChat(player, "You need to have the Extra Pockets buff in order to access this pouch.");
            }
        }

        bool FetchItems(BasePlayer player, ItemContainer container)
        {
            if (player.IsDead() || !player.IsConnected) return false;
            PlayerInfo playerData;
            if (pcdData.pEntity.TryGetValue(player.userID, out playerData))
            {
                if (playerData.pouch_items == null || playerData.pouch_items.Count == 0) return true;
                foreach (var item in playerData.pouch_items)
                {
                    GetRestoreItem(player, container, item);
                }
                playerData.pouch_items.Clear();
            }

            return true;
        }

        Item GetRestoreItem(BasePlayer player, ItemContainer container, ItemInfo savedItem)
        {
            var item = ItemManager.CreateByName(savedItem.shortname, savedItem.amount, savedItem.skin);
            if (savedItem.name != null) item.name = savedItem.name;
            if (savedItem.text != null) item.text = savedItem.text;
            item.condition = savedItem.condition;
            item.maxCondition = savedItem.maxCondition;
            BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
            if (weapon != null)
            {
                if (!string.IsNullOrEmpty(savedItem.ammotype))
                    weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(savedItem.ammotype);
                weapon.primaryMagazine.contents = savedItem.ammo;
            }
            FlameThrower flameThrower = item.GetHeldEntity() as FlameThrower;
            if (flameThrower != null) flameThrower.ammo = savedItem.ammo;
            if (savedItem.instanceData != null)
            {
                item.instanceData = new ProtoBuf.Item.InstanceData();
                item.instanceData.ShouldPool = false;
                item.instanceData.dataInt = savedItem.instanceData.dataInt;
                item.instanceData.blueprintTarget = savedItem.instanceData.blueprintTarget;
                item.instanceData.blueprintAmount = savedItem.instanceData.blueprintAmount;
            }
            if (savedItem.item_contents != null && savedItem.item_contents.Count > 0)
            {
                if (item.contents == null)
                {
                    item.contents = new ItemContainer();
                    item.contents.ServerInitialize(null, savedItem.item_contents.Count);
                    item.contents.GiveUID();
                    item.contents.parent = item;
                }
                foreach (var _item in savedItem.item_contents)
                {
                    GetRestoreItem(player, item.contents, _item);
                }
            }
            if (!item.MoveToContainer(container, savedItem.position)) player.GiveItem(item);
            return item;
        }

        StorageContainer GenerateBag(BasePlayer player, int slots)
        {
            var pos = new Vector3(player.transform.position.x, player.transform.position.y - 1000, player.transform.position.z);
            var storage = GameManager.server.CreateEntity(config.buff_settings.bag_prefab, pos) as StorageContainer;
            storage.Spawn();            

            UnityEngine.Object.DestroyImmediate(storage.GetComponent<GroundWatch>());
            UnityEngine.Object.DestroyImmediate(storage.GetComponent<DestroyOnGroundMissing>());

            storage.inventory.capacity = slots;
            storage.inventorySlots = slots;

            FetchItems(player, storage.inventory);
            storage.OwnerID = player.userID;
            containers.Add(storage.inventory.uid, new Containers(storage, player.UserIDString, player.userID));
            return storage;
        }

        //List<StorageContainer> containers = new List<StorageContainer>();

        Dictionary<uint, Containers> containers = new Dictionary<uint, Containers>();
        public class Containers
        {
            public StorageContainer container;
            public string userIDString;
            public ulong userID;
            public Containers(StorageContainer container, string userIDString, ulong userID)
            {
                this.container = container;
                this.userIDString = userIDString;
                this.userID = userID;
            }
        }

        private object False = false;
        private object True = true;

        [HookMethod("IsExtraPocketsContainer")]
        public object IsExtraPocketsContainer(uint uid)
        {
            if (containers.ContainsKey(uid)) return True;            
            return False;
        }

        [HookMethod("GetExtraPocketsContainerProvider")]
        public Func<uint, bool> GetExtraPocketsContainerProvider()
        {
            return new Func<uint, bool>(uid =>
            {
                if (containers.ContainsKey(uid)) return true;               
                return false;
            });
        }

        [HookMethod("GetExtraPocketsOwnerIdProvider")]
        public Func<uint, string> GetExtraPocketsOwnerIdProvider()
        {
            return new Func<uint, string>(uid =>
            {
                Containers data;
                if (containers.TryGetValue(uid, out data)) return data.userIDString;
                return null;
            });
        }

        bool StorePlayerItems(BasePlayer player, StorageContainer container)
        {
            PlayerInfo playerData;
            if (!pcdData.pEntity.TryGetValue(player.userID, out playerData))
            {
                if (player.IsConnected) PrintToChat(player, lang.GetMessage("FailReload", this, player.UserIDString));
                //LogToFile("DataFailure", $"[{DateTime.Now}] Failed to acquire data for {player.displayName}[{player.UserIDString}] - StorePlayerItems. [Online = {player.IsConnected}]", this, true);
                return false;
            }
            List<Item> items = Pool.GetList<Item>();
            items.AddRange(container.inventory?.itemList);
            var droppedItemsStr = "";
            foreach (var item in items)
            {
                if (config.tools_black_white_list_settings.white_list.Count > 0)
                {
                    if (!config.tools_black_white_list_settings.white_list.Contains(item.info.shortname))
                    {
                        player.GiveItem(item);
                        droppedItemsStr += $"{item.name ?? item.info.displayName.english}\n";
                    }
                }
                else if (config.tools_black_white_list_settings.black_list.Contains(item.info.shortname))
                {
                    player.GiveItem(item);
                    droppedItemsStr += $"{item.name ?? item.info.displayName.english}\n";
                }
            }

            if (!string.IsNullOrEmpty(droppedItemsStr))
            {
                if (config.tools_black_white_list_settings.white_list.Count > 0) PrintToChat(player, string.Format(lang.GetMessage("WhitelistedItemsNotFound", this, player.UserIDString), droppedItemsStr));
                else PrintToChat(player, string.Format(lang.GetMessage("BlacklistedItemsFound", this, player.UserIDString), droppedItemsStr));
            }

            Pool.FreeList(ref items);

            playerData.pouch_items.AddRange(GetItems(player, container.inventory));

            containers.Remove(container.inventory.uid);
            container.Invoke(container.KillMessage, 0.01f);

            return true;
        }

        List<ItemInfo> GetItems(BasePlayer player, ItemContainer container)
        {
            List<ItemInfo> result = new List<ItemInfo>();
            foreach (var item in container.itemList)
            {
                result.Add(new ItemInfo()
                {
                    shortname = item.info.shortname,
                    position = item.position,
                    amount = item.amount,
                    ammo = item.GetHeldEntity() is BaseProjectile ? (item.GetHeldEntity() as BaseProjectile).primaryMagazine.contents : item.GetHeldEntity() is FlameThrower ? (item.GetHeldEntity() as FlameThrower).ammo : 0,
                    ammotype = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.ammoType.shortname ?? null,
                    skin = item.skin,
                    condition = item.condition,
                    maxCondition = item.maxCondition,
                    instanceData = item.instanceData != null ? new ItemInfo.KeyInfo()
                    {
                        dataInt = item.instanceData.dataInt,
                        blueprintTarget = item.instanceData.blueprintTarget,
                        blueprintAmount = item.instanceData.blueprintAmount,
                    }
                    : null,
                    name = item.name ?? null,
                    text = item.text ?? null,
                    item_contents = item.contents?.itemList != null ? GetItems(player, item.contents) : null
                });
            }
            return result;
        }

        void OnLootEntityEnd(BasePlayer player, StorageContainer container)
        {
            if (player == null || container == null || container.inventory == null) return;
            if (containers.ContainsKey(container.inventory.uid))
            {
                StorePlayerItems(player, container);
            }
        }

        void OnEntityKill(StorageContainer container)
        {
            if (container != null && containers.ContainsKey(container.inventory.uid))
            {
                var player = BasePlayer.activePlayerList.Where(x => x.userID == container.OwnerID).FirstOrDefault();
                if (player == null) return;
                StorePlayerItems(player, container);
            }
        }

        bool CheckedButtonReady = false;
        List<BasePlayer> waitingPlayers = new List<BasePlayer>();
        Timer checkTimer;

        void CheckButtonReady()
        {
            PlayerInfo playerData;
            if (config.general_settings.image_cache_source.Equals("url", StringComparison.OrdinalIgnoreCase))
            {
                if (Convert.ToBoolean(ImageLibrary?.Call("HasImage", ExtraPocketsImg.Key)))
                {
                    CheckedButtonReady = true;
                    foreach (var player in waitingPlayers)
                    {
                        if (pcdData.pEntity.TryGetValue(player.userID, out playerData) && playerData.extra_pockets_button)
                            SendExtraPocketsButton(player);
                    }
                    waitingPlayers.Clear();
                    waitingPlayers = null;
                    checkTimer = null;
                }
                else
                {
                    checkTimer = timer.In(5f, CheckButtonReady);
                }
            }
            else
            {
                CheckedButtonReady = true;
                foreach (var player in waitingPlayers)
                {
                    if (pcdData.pEntity.TryGetValue(player.userID, out playerData) && playerData.extra_pockets_button)
                        SendExtraPocketsButton(player);
                }
                waitingPlayers.Clear();
                waitingPlayers = null;
                if (checkTimer != null && !checkTimer.Destroyed) checkTimer.Destroy();
                checkTimer = null;
            }
        }

        private void SendExtraPocketsButton(BasePlayer player)
        {
            if (string.IsNullOrEmpty(ExtraPocketsImg.Key) && string.IsNullOrEmpty(ExtraPocketsImgSkin.Key)) return;
            if (!CheckedButtonReady)
            {
                if (!waitingPlayers.Contains(player)) waitingPlayers.Add(player);
                if (checkTimer == null || checkTimer.Destroyed) CheckButtonReady();
                return;
            }

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.3207547 0.3207547 0.3207547 0.6705883" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = config.tools_black_white_list_settings.extra_pockets_button_anchor.x_min + " " + config.tools_black_white_list_settings.extra_pockets_button_anchor.y_min, OffsetMax = config.tools_black_white_list_settings.extra_pockets_button_anchor.x_max + " " + config.tools_black_white_list_settings.extra_pockets_button_anchor.y_max }
            }, "ContentUI", "ExtraPocketsButton");

            if (config.general_settings.image_cache_source.Equals("url", StringComparison.OrdinalIgnoreCase) || ExtraPocketsImgSkin.Value == 0)
            {
                container.Add(new CuiElement
                {
                    Name = "Image_5410",
                    Parent = "ExtraPocketsButton",
                    Components = {
                    new CuiRawImageComponent { Color = "1 1 1 1", Png = (string)ImageLibrary?.Call("GetImage", ExtraPocketsImg.Key) },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-19 -19", OffsetMax = "19 19" }
                }
                });
            }
            else
            {
                container.Add(new CuiElement
                {
                    Name = "Image_5410",
                    Parent = "ExtraPocketsButton",
                    Components = {
                    new CuiImageComponent { Color = "1 1 1 1", ItemId = 1751045826, SkinId = ExtraPocketsImgSkin.Value },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-19 -19", OffsetMax = "19 19" }
                }
                });
            }    

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = $"openextrapockets" },
                Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-21 -21", OffsetMax = "21 21" }
            }, "ExtraPocketsButton", "Button_2451");

            CuiHelper.DestroyUi(player, "ExtraPocketsButton");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("openextrapockets")]
        void OpenExtraPocketsButton(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            OpenBag(player);
        }

        #endregion

        #region Scoreboard

        void CheckScoreBoardConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            CheckScoreBoard(player);
        }

        void CheckScoreBoard(BasePlayer player)
        {
            if (ScoreBoard.lastChecked + 10f < Time.time) UpdateScoreBoard();
            ScoreBoardBackPanel(player);
            ScoreBoardPanel(player);
        }

        void UpdateScoreBoard()
        {
            if (pcdData.pEntity.Count == 0) return;
            ScoreBoard.scoreList = pcdData.pEntity.OrderByDescending(x => x.Value.xp).ToDictionary(i => i.Key, i => new ScoreboardInfo.ScoreInfo(i.Value.name ?? i.Key.ToString(), Math.Round(i.Value.xp, config.xp_settings.xp_rounding)));
            List<ulong> keys_to_remove = Pool.GetList<ulong>();
            foreach (var kvp in ScoreBoard.scoreList)
            {
                if (permission.UserHasPermission(kvp.Key.ToString(), perm_no_scoreboard))
                    keys_to_remove.Add(kvp.Key);
            }

            foreach (var key in keys_to_remove)
                ScoreBoard.scoreList.Remove(key);

            Pool.FreeList(ref keys_to_remove);
            ScoreBoard.lastChecked = Time.time;
        }

        private void ScoreBoardBackPanel(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0.99" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-0.287 0.343", OffsetMax = "0.312 -0.337" }
            }, "ContentUI", "ScoreboardBackPanel");

            CuiHelper.DestroyUi(player, "ScoreboardBackPanel");
            CuiHelper.AddUi(player, container);
        }

        // Max 15 per page.
        private void ScoreBoardPanel(BasePlayer player, int lastElement = 0)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-231.299 -189.993", OffsetMax = "128.701 190.007" }
            }, "ContentUI", "ScoreBoardPanel");
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1698113 0.1698113 0.1698113 0.6980392" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-176 -186.26", OffsetMax = "176 139.74" }
            }, "ScoreBoardPanel", "Panel_7832");
            container.Add(new CuiElement
            {
                Name = "Label_1767",
                Parent = "ScoreBoardPanel",
                Components = {
                    new CuiTextComponent { Text = "SCORES", Font = "robotocondensed-bold.ttf", FontSize = 32, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-116.001 144", OffsetMax = "115.999 186" }
                }
            });
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1226415 0.122063 0.122063 0.8" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-176.001 120.246", OffsetMax = "175.999 139.735" }
            }, "ScoreBoardPanel", "Panel_2861");
            container.Add(new CuiElement
            {
                Name = "Label_2820",
                Parent = "Panel_2861",
                Components = {
                    new CuiTextComponent { Text = "RANK", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.2832619 0.5943396 0 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-174.309 -9.741", OffsetMax = "-139.689 9.75" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Label_4187",
                Parent = "Panel_2861",
                Components = {
                    new CuiTextComponent { Text = "NAME", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.9150943 0.8035371 0 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-97.482 -9.746", OffsetMax = "2.518 9.745" }
                }
            });
            container.Add(new CuiElement
            {
                Name = "Label_8268",
                Parent = "Panel_2861",
                Components = {
                    new CuiTextComponent { Text = "TOTAL XP", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "0 0.8679245 0.8500054 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "42.741 -9.746", OffsetMax = "176.001 9.745" }
                }
            });
            var count = 0;
            var lastEntry = lastElement;
            for (int i = lastElement; i < lastElement + 15; i++)
            {
                if (i < 0)
                {
                    continue;
                }
                if (ScoreBoard.scoreList.Count <= i)
                {
                    break;
                }
                container.Add(new CuiElement
                {
                    Name = "ScoreRank",
                    Parent = "ScoreBoardPanel",
                    Components = {
                    new CuiTextComponent { Text = $"{i+1}:", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperLeft, Color = "0.282353 0.5960785 0 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-172.019 {94.5 - (20 * count)}", OffsetMax = $"-141.981 {114.5 - (20 * count)}" }
                }
                });
                container.Add(new CuiElement
                {
                    Name = "ScoreName",
                    Parent = "ScoreBoardPanel",
                    Components = {
                    new CuiTextComponent { Text = ScoreBoard.scoreList.ElementAt(i).Value.name, Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperCenter, Color = "0.9137255 0.8039216 0 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"-137.399 {94.5 - (20 * count)}", OffsetMax = $"42.433 {114.5 - (20 * count)}" }
                }
                });

                container.Add(new CuiElement
                {
                    Name = "ScoreXP",
                    Parent = "ScoreBoardPanel",
                    Components = {
                    new CuiTextComponent { Text = $"{ScoreBoard.scoreList.ElementAt(i).Value.xp}", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperLeft, Color = "0 0.8666667 0.8509804 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"42.741 {94.5 - (20 * count)}", OffsetMax = $"175.999 {114.5 - (20 * count)}" }
                }
                });
                count++;
                lastEntry = i;
            }
            if (lastEntry - 15 > 0)
            {
                container.Add(new CuiElement
                {
                    Name = "leftarrow",
                    Parent = "ScoreBoardPanel",
                    Components = {
                    new CuiTextComponent { Text = "<<", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-176 149", OffsetMax = "-144 181" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 1 1 0", Command = $"scoreboardchangepage {lastElement - 15}" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-16 -16", OffsetMax = "16 16" }
                }, "leftarrow", "Button_4851");
            }
            if (ScoreBoard.scoreList.Count - 1 > lastEntry)
            {
                container.Add(new CuiElement
                {
                    Name = "rightarrow",
                    Parent = "ScoreBoardPanel",
                    Components = {
                    new CuiTextComponent { Text = ">>", Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "144 150.8", OffsetMax = "176 182.8" }
                }
                });

                container.Add(new CuiButton
                {
                    Button = { Color = "1 1 1 0", Command = $"scoreboardchangepage {lastEntry + 1}" },
                    Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-16 -16", OffsetMax = "16 16" }
                }, "rightarrow", "Button_4851123123");
            }
            container.Add(new CuiElement
            {
                Name = "ScoreboardCloseLabel",
                Parent = "ScoreBoardPanel",
                Components = {
                    new CuiTextComponent { Text = "CLOSE", Font = "robotocondensed-regular.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-26 -219.8", OffsetMax = "26 -195.8" }
                }
            });
            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = "closescoreboard" },
                Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-26 -12.001", OffsetMax = "26 12.001" }
            }, "ScoreboardCloseLabel", "ScoreboardCloseButton");
            CuiHelper.DestroyUi(player, "ScoreBoardPanel");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("closescoreboard")]
        void CloseScoreBoard(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            CuiHelper.DestroyUi(player, "ScoreboardBackPanel");
            CuiHelper.DestroyUi(player, "ScoreBoardPanel");
        }

        [ConsoleCommand("scoreboardchangepage")]
        void ChangeBoard(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            var startElement = Convert.ToInt32(arg.Args[0]);

            // if the element is on the first page, we just show the whole first page.
            if (startElement > 0 && startElement < 15) ScoreBoardPanel(player, 0);
            // If the element is larger than the list count, we send the last 15 players in the list.
            else if (startElement > ScoreBoard.scoreList.Count - 1) ScoreBoardPanel(player, ScoreBoard.scoreList.Count - 16);
            // Otherwise we send the scoreboard from the startElement.
            else ScoreBoardPanel(player, startElement);
        }

        #endregion

        #region Ultimate settings

        string GetUltimateSettingsDescription(Buff buff)
        {
            switch (buff)
            {
                case Buff.Woodcutting_Ultimate: return string.Format(lang.GetMessage("UltimateSettingsUIDescription", this), buff.ToString().Split('_')[0]);
                case Buff.Mining_Ultimate: return string.Format(lang.GetMessage("UltimateSettingsUIDescription", this), buff.ToString().Split('_')[0]);
                case Buff.Combat_Ultimate: return string.Format(lang.GetMessage("UltimateSettingsUIDescription", this), buff.ToString().Split('_')[0]);
                case Buff.Vehicle_Ultimate: return string.Format(lang.GetMessage("UltimateSettingsUIDescription", this), buff.ToString().Split('_')[0]);
                case Buff.Harvester_Ultimate: return string.Format(lang.GetMessage("UltimateSettingsUIDescription", this), buff.ToString().Split('_')[0]);
                case Buff.Medical_Ultimate: return string.Format(lang.GetMessage("UltimateSettingsUIDescription", this), buff.ToString().Split('_')[0]);
                case Buff.Skinning_Ultimate: return string.Format(lang.GetMessage("UltimateSettingsUIDescription", this), buff.ToString().Split('_')[0]);
                case Buff.Build_Craft_Ultimate: return string.Format(lang.GetMessage("UltimateSettingsUIDescription", this), lang.GetMessage("Build_Craft_formatted", this));
                case Buff.Scavengers_Ultimate: return string.Format(lang.GetMessage("UltimateSettingsUIDescription", this), buff.ToString().Split('_')[0]);
                default: return "Custom Ultimate";
            }
        }

        private void SkillTree_UltimateMenu(BasePlayer player)
        {
            PlayerInfo pi;
            if (!pcdData.pEntity.TryGetValue(player.userID, out pi)) return;

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0.9803922" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0.024 -0.331", OffsetMax = "0.325 0.339" }
            }, "ContentUI", "SkillTree_UltimateMenu");

            container.Add(new CuiElement
            {
                Name = "SkillTreeUltimateMenu_Title",
                Parent = "SkillTree_UltimateMenu",
                Components = {
                    new CuiTextComponent { Text = "Ultimate Settings", Font = "robotocondensed-bold.ttf", FontSize = 26, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-495.24 190.4", OffsetMax = "-180.18 250.4" }
                }
            });

            var count = 0;
            var row = 0;
            if (pi.ultimate_settings.Count == 0)
            {
                container.Add(new CuiElement
                {
                    Name = "no_ultimates_unlocked",
                    Parent = "SkillTree_UltimateMenu",
                    Components = {
                    new CuiTextComponent { Text = "You do not have any ultimate abilities unlocked.", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 0.9397521 0 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-495.24 158.268", OffsetMax = "-180.18 190.4" }
                }
                });
            }
            else foreach(var option in pi.ultimate_settings)
            {
                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1132075 0.1073335 0.1073335 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{-495.24 + (325.06 * row)} {132.4 - (68 * count)}", OffsetMax = $"{-180.18 + (325.06 * row)} {190.4 - (68 * count)}" }
                }, "SkillTree_UltimateMenu", "SkillTree_UltimateMenu_tgl_pnl_bk_1");

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.2264151 0.2264151 0.2264151 0.9607843" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-155.53 -27", OffsetMax = "155.53 27" }
                }, "SkillTree_UltimateMenu_tgl_pnl_bk_1", "SkillTree_UltimateMenu_tgl_pnl_ft_1");

                container.Add(new CuiElement
                {
                    Name = "SkillTree_UltimateMenu_tgl_des_1",
                    Parent = "SkillTree_UltimateMenu_tgl_pnl_bk_1",
                    Components = {
                    new CuiTextComponent { Text = GetUltimateSettingsDescription(option.Key), Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-147 -26", OffsetMax = "52.24 26" }
                }
                });

                container.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image = { Color = "0.1132075 0.1073335 0.1073335 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "66.53 -24", OffsetMax = "152.53 24" }
                }, "SkillTree_UltimateMenu_tgl_pnl_bk_1", "SkillTree_UltimateMenu_tgl_bttn_pnl_1");

                container.Add(new CuiButton
                {
                    Button = { Color = "0.1607843 0.1607843 0.1607843 1", Command = $"sttoggleultimate {option.Key}" },
                    Text = { Text = option.Value.enabled ? lang.GetMessage("ON", this, player.UserIDString) : lang.GetMessage("OFF", this, player.UserIDString), Font = "robotocondensed-regular.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = option.Value.enabled ? "0.1333333 0.5960785 0.1872104 1" : "0.5943396 0.131764 0.1842591 1" },
                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "68.53 -22", OffsetMax = "150.53 22" }
                }, "SkillTree_UltimateMenu_tgl_pnl_bk_1", "SkillTree_UltimateMenu_tgl_bttn_1");

                count++;
                if (count >= 5)
                {
                    row++;
                    count = 0;
                }
            }

            container.Add(new CuiElement
            {
                Name = "SkillTree_UltimateMenu_close",
                Parent = "SkillTree_UltimateMenu",
                Components = {
                    new CuiTextComponent { Text = "CLOSE", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-366.71 -201.1", OffsetMax = "-308.71 -169.1" }
                }
            });

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = $"stcloseultimatesettings" },
                Text = { Text = " ", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-29 -16", OffsetMax = "29 16" }
            }, "SkillTree_UltimateMenu_close", "SkillTree_UltimateMenu_close_button");



            CuiHelper.DestroyUi(player, "SkillTree_UltimateMenu");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("stcloseultimatesettings")]
        void CloseUltimateSettings(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            CuiHelper.DestroyUi(player, "SkillTree_UltimateMenu");
        }

        [ConsoleCommand("sttoggleultimate")]
        void ToggleUltimate(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            PlayerInfo pi;
            if (!pcdData.pEntity.TryGetValue(player.userID, out pi)) return;
            Buff buff;
            if (!Enum.TryParse(arg.Args[0], out buff)) return;

            if (!pi.ultimate_settings.ContainsKey(buff)) pi.ultimate_settings.Add(buff, new UltimatePlayerSettings());
            if (pi.ultimate_settings[buff].enabled) pi.ultimate_settings[buff].enabled = false;
            else pi.ultimate_settings[buff].enabled = true;

            HandleUltimateToggle(player, buff, pi);

            SkillTree_UltimateMenu(player);
        }

        bool IsUltimateEnabled(BasePlayer player, Buff buff)
        {
            return pcdData.pEntity.ContainsKey(player.userID) && pcdData.pEntity[player.userID].ultimate_settings.ContainsKey(buff) && pcdData.pEntity[player.userID].ultimate_settings[buff].enabled;
        }

        void HandleUltimateToggle(BasePlayer player, Buff buff, PlayerInfo pi)
        {
            // Add handles here.

            UltimatePlayerSettings ups;
            if (!pi.ultimate_settings.TryGetValue(buff, out ups)) pi.ultimate_settings.Add(buff, ups = new UltimatePlayerSettings());
            if (!ups.enabled) PrintToChat(player, $"Ultimate: {buff} has been disabled.");

            if (buff == Buff.Mining_Ultimate)
            {
                if (ups.enabled)
                {
                    PrintToChat(player, string.Format(lang.GetMessage("UltimateToggleOnMining", this, player.UserIDString), config.ultimate_settings.ultimate_mining.distance_from_player, config.ultimate_settings.ultimate_mining.find_node_cmd, config.ultimate_settings.ultimate_mining.cooldown));
                }
            }
            if (buff == Buff.Vehicle_Ultimate)
            {
                if (ups.enabled)
                {
                    PrintToChat(player, string.Format(lang.GetMessage("UltimateToggleOnVehicle", this, player.UserIDString), config.ultimate_settings.ultimate_vehicle.reduce_by * 100));
                }
            }
            if (buff == Buff.Medical_Ultimate)
            {
                if (ups.enabled)
                {
                    PrintToChat(player, string.Format(lang.GetMessage("UltimateToggleOnMedical", this, player.UserIDString), config.ultimate_settings.ultimate_medical.resurrection_chance));
                }
                else CuiHelper.DestroyUi(player, "SkillTree_MedicalUltimate_ResurrectionButton");
            }
            if (buff == Buff.Harvester_Ultimate)
            {
                if (ups.enabled)
                {
                    PrintToChat(player, string.Format(lang.GetMessage("UltimateToggleOnHarvester", this, player.UserIDString), config.ultimate_settings.ultimate_harvesting.gene_chat_command, config.ultimate_settings.ultimate_harvesting.cooldown));
                }
            }

            if (buff == Buff.Build_Craft_Ultimate)
            {
                if (ups.enabled)
                {
                    PrintToChat(player, string.Format(lang.GetMessage("UltimateToggleOnBuildCraft", this, player.UserIDString), config.ultimate_settings.ultimate_buildCraft.success_chance));
                }
            }

            if (buff == Buff.Woodcutting_Ultimate)
            {
                if (ups.enabled)
                {
                    PrintToChat(player, string.Format(lang.GetMessage("UltimateToggleOnWoodcutting", this, player.UserIDString), config.ultimate_settings.ultimate_woodcutting.distance_from_player));
                }
            }

            if (buff == Buff.Scavengers_Ultimate)
            {
                if (ups.enabled)
                {
                    PrintToChat(player, lang.GetMessage("UltimateToggleOnScavengers", this, player.UserIDString));
                }
            }
            
            if (buff == Buff.Combat_Ultimate)
            {
                if (ups.enabled)
                {
                    string formattedString = "";
                    var active = 0;
                    if (config.ultimate_settings.ultimate_combat.scientists_enabled) active++;
                    if (config.ultimate_settings.ultimate_combat.players_enabled) active++;
                    if (config.ultimate_settings.ultimate_combat.scientists_enabled) active++;

                    if (config.ultimate_settings.ultimate_combat.scientists_enabled)
                    {
                        formattedString += "<color=#DFF008>Scientists</color>";
                        if (active == 2) formattedString += " and ";
                        if (active == 3) formattedString += ", ";
                    }
                    if (config.ultimate_settings.ultimate_combat.animals_enabled)
                    {
                        formattedString += "<color=#DFF008>Animals</color>";
                        if (active == 3) formattedString += " and ";
                    }
                    if (config.ultimate_settings.ultimate_combat.players_enabled) formattedString += "<color=#DFF008>Players</color>";
                    PrintToChat(player, $"You will now receive <color=#DFF008>{config.ultimate_settings.ultimate_combat.health_scale * 100}%</color> of the damage as health when damaging {formattedString}.");
                }
            }
            if (buff == Buff.Skinning_Ultimate)
            {
                if (ups.enabled)
                {
                    PrintToChat(player, $"You will now receive receive perks when killing a: <color=#DFF008>{string.Join("</color>, <color=#DFF008>", config.ultimate_settings.ultimate_skinning.enabled_buffs.Select(x => x.Key.ToString()))}.</color>");
                }
                else
                {
                    RemoveAnimalBuff(player);
                }
            }
        }

        #endregion

        #region Mining ultimate

        Dictionary<ulong, float> MiningUltimateCooldowns = new Dictionary<ulong, float>();

        void TriggerMiningUltimateFromItem(BasePlayer player) => TriggerMiningUltimateAction(player, false);
        void TriggerMiningUltimateFromCMD(BasePlayer player) => TriggerMiningUltimateAction(player, true);

        void TriggerMiningUltimateAction(BasePlayer player, bool from_command = true)
        {
            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.ContainsKey(Buff.Mining_Ultimate) && IsUltimateEnabled(player, Buff.Mining_Ultimate))
            {
                if (MiningUltimateCooldowns.ContainsKey(player.userID))
                {
                    if (MiningUltimateCooldowns[player.userID] > Time.time)
                    {
                        if (from_command) PrintToChat(player, $"You are still on cooldown from the last time you used this ability. Please wait {Math.Round(MiningUltimateCooldowns[player.userID] - Time.time, 0)} seconds before trying again.");
                        return;
                    }
                    else MiningUltimateCooldowns[player.userID] = Time.time + config.ultimate_settings.ultimate_mining.cooldown;
                }
                else MiningUltimateCooldowns.Add(player.userID, Time.time + config.ultimate_settings.ultimate_mining.cooldown);
                List<BaseEntity> mining_nodes = Pool.GetList<BaseEntity>();
                mining_nodes.AddRange(FindEntitiesOfType<BaseEntity>(player.transform.position, config.ultimate_settings.ultimate_mining.distance_from_player).Where(x => x.PrefabName.StartsWith("assets/bundled/prefabs/autospawn/resource/ores")));
                if (mining_nodes.Count > 0)
                {
                    var wasAdmin = player.IsAdmin;
                    if (!wasAdmin)
                    {
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                        player.SendNetworkUpdateImmediate();
                    }

                    string nodeName;
                    foreach (var node in mining_nodes)
                    {
                        nodeName = string.Format("<size={0}>{1}</size>", config.ultimate_settings.ultimate_mining.text_size, node.ShortPrefabName == "metal-ore" ? "metal" : node.ShortPrefabName == "stone-ore" ? "stone" : "sulfur");
                        player.SendConsoleCommand("ddraw.text", config.ultimate_settings.ultimate_mining.hud_time, Color.yellow, node.transform.position, nodeName);
                    }

                    if (!wasAdmin)
                    {
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                        player.SendNetworkUpdateImmediate();
                    }
                }
                Pool.FreeList(ref mining_nodes);
            }
        }

        #endregion

        #region Resurrection button

        private void SendResurrectionButton(BasePlayer player, Vector3 pos)
        {
            if (Resurrection_Cooldowns.ContainsKey(player.userID) && Resurrection_Cooldowns[player.userID] > Time.time) return;

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1320755 0.1314525 0.1314525 1" },
                RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-104.9 86.718", OffsetMax = "-4.9 110.718" }
            }, "ContentUI", "SkillTree_MedicalUltimate_ResurrectionButton");

            container.Add(new CuiButton
            {
                Button = { Color = "0.2352941 0.2705882 0.1607843 1", Command = $"stattemptresurrection {pos.x} {pos.y} {pos.z}" },
                Text = { Text = "RESURRECT", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "0.5803922 0.7294118 0.2588235 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-48 -10", OffsetMax = "48 10" }
            }, "SkillTree_MedicalUltimate_ResurrectionButton", "Button_2968");

            CuiHelper.DestroyUi(player, "SkillTree_MedicalUltimate_ResurrectionButton");
            CuiHelper.AddUi(player, container);
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "SkillTree_MedicalUltimate_ResurrectionButton");
        }

        Dictionary<ulong, float> Resurrection_Cooldowns = new Dictionary<ulong, float>();

        [ConsoleCommand("stattemptresurrection")]
        void AttemptResurrection(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            CuiHelper.DestroyUi(player, "SkillTree_MedicalUltimate_ResurrectionButton");

            if (player.IsAlive() || !player.IsConnected) return;

            if (UnityEngine.Random.Range(0f, 100f) <= config.ultimate_settings.ultimate_medical.resurrection_chance)
            {
                var pos = new Vector3(Convert.ToSingle(arg.Args[0]), Convert.ToSingle(arg.Args[1]), Convert.ToSingle(arg.Args[2]));
                player.RespawnAt(pos, Quaternion.identity);
                if (Resurrection_Cooldowns.ContainsKey(player.userID)) Resurrection_Cooldowns[player.userID] = Time.time + config.ultimate_settings.ultimate_medical.resurrection_delay;
                else Resurrection_Cooldowns.Add(player.userID, Time.time + config.ultimate_settings.ultimate_medical.resurrection_delay);
            }
            else
            {
                SendResurrectionFailed(player);
                timer.Once(3f, () =>
                {
                    if (player != null)
                        CuiHelper.DestroyUi(player, "SkillTree_MedicalUltimate_Failed");
                });
            }
        }

        private void SendResurrectionFailed(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = "SkillTree_MedicalUltimate_Failed",
                Parent = "ContentUI",
                Components = {
                    new CuiTextComponent { Text = "FAILED", Font = "robotocondensed-bold.ttf", FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "1 0 0 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-104.57 66.393", OffsetMax = "-4.57 90.387" }
                }
            });

            CuiHelper.DestroyUi(player, "SkillTree_MedicalUltimate_Failed");
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Harvester Ultimate

        bool FoundInvalidGeneLetter(string genes)
        {
            foreach (var c in genes.ToCharArray())
            {
                if (c != 'g' && c != 'e' && c != 'x' && c != 'w' && c != 'y' && c != 'h') return true;
            }
            return false;
        }

        void SetPlantGenes(BasePlayer player, string command, string[] args)
        {
            BuffDetails bd;
            if (!buffDetails.TryGetValue(player.userID, out bd) || !bd.buff_values.ContainsKey(Buff.Harvester_Ultimate))
            {
                PrintToChat(player, "You need to have unlocked the Harvesting ultimate in order to use this command.");
                return;
            }
            Plant_Gene_Select_background(player);
            Plant_Gene_Select(player);
        }

        private void Plant_Gene_Select(BasePlayer player)
        {
            PlayerInfo pi;
            if (!pcdData.pEntity.TryGetValue(player.userID, out pi)) return;

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0.9490196" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-0.675 0.01", OffsetMax = "0.325 0" }
            }, "ContentUI", "Plant_Gene_Select");

            container.Add(new CuiElement
            {
                Name = "title",
                Parent = "Plant_Gene_Select",
                Components = {
                    new CuiTextComponent { Text = "PLANT GENE STRUCTURE", Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-128 160.3", OffsetMax = "128 192.3" }
                }
            });

            var gene_array = pi.plant_genes.ToCharArray();
            char[] gene_chars = { 'g', 'x', 'w', 'y', 'h' };
            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    string buttonCol = "0.2924528 0.2924528 0.2924528 1";
                    if (gene_array[i] == gene_chars[j]) buttonCol = "0.003070482 0.2169811 0.02914789 1";
                    container.Add(new CuiPanel
                    {
                        CursorEnabled = false,
                        Image = { Color = "0.1698113 0.1698113 0.1698113 0.8" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{-90 + (37 * j)} {120.2 - (37 * i)}", OffsetMax = $"{-58 + (37 * j)} {152.2 - (37 * i)}" }
                    }, "Plant_Gene_Select", "gene_button_panel");

                    container.Add(new CuiButton
                    {
                        Button = { Color = buttonCol, Command = $"setgenevalue {i} {gene_chars[j]}" },
                        Text = { Text = gene_chars[j].ToString().ToUpper(), Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-14 -14", OffsetMax = "14 14" }
                    }, "gene_button_panel", "gene_button");
                }                
            }

            container.Add(new CuiButton
            {
                Button = { Color = "1 1 1 0", Command = "closegenstructuremenu" },
                Text = { Text = "X", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "128 160.3", OffsetMax = "160 192.3" }
            }, "Plant_Gene_Select", "close");

            CuiHelper.DestroyUi(player, "Plant_Gene_Select");
            CuiHelper.AddUi(player, container);
        }

        private void Plant_Gene_Select_background(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0.9490196" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "-0.675 0.01", OffsetMax = "0.325 0" }
            }, "ContentUI", "Plant_Gene_Select_background");

            CuiHelper.DestroyUi(player, "Plant_Gene_Select_background");
            CuiHelper.AddUi(player, container);
        }

        [ConsoleCommand("closegenstructuremenu")]
        void CloseGeneMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            CuiHelper.DestroyUi(player, "Plant_Gene_Select");
            CuiHelper.DestroyUi(player, "Plant_Gene_Select_background");
        }

        [ConsoleCommand("setgenevalue")]
        void ChangeChar(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            CuiHelper.DestroyUi(player, "Plant_Gene_Select");

            var pos = Convert.ToInt32(arg.Args[0]);
            var c = Convert.ToChar(arg.Args[1]);

            PlayerInfo pi;
            if (pcdData.pEntity.TryGetValue(player.userID, out pi))
            {
                string newGene = "";
                for (int i = 0; i < 6; i++)
                {
                    if (i == pos) newGene += c;
                    else newGene += pi.plant_genes[i];
                }
                pi.plant_genes = newGene.ToString();
            }
            Plant_Gene_Select(player);
        }

        #endregion

        #region Scavengers Ultimate

        private const int maxRoll = 100;

        private void Card(BasePlayer player, int lvl)
        {
            switch (lvl)
            {
                case 1:
                    AwardXP(player, config.xp_settings.xp_sources.swipe_card_level_1);
                    break;
                case 2:
                    AwardXP(player, config.xp_settings.xp_sources.swipe_card_level_2);
                    break;
                case 3:
                    AwardXP(player, config.xp_settings.xp_sources.swipe_card_level_3);
                    break;
            }
        }

        private object OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {
            Item item = card.GetItem();
            if (item == null || item.isBroken || cardReader == null || player == null) return null;

            BuffDetails bd;
            if (card.accessLevel != cardReader.accessLevel && buffDetails.TryGetValue(player.userID, out bd) && bd.buff_values.ContainsKey(Buff.Build_Craft_Ultimate) && IsUltimateEnabled(player, Buff.Build_Craft_Ultimate))
            {
                var roll = UnityEngine.Random.Range(1, maxRoll + 1);
                if (config.ultimate_settings.ultimate_buildCraft.success_chance < maxRoll && maxRoll - config.ultimate_settings.ultimate_buildCraft.success_chance < roll)
                {
                    if (config.ultimate_settings.ultimate_buildCraft.notify_fail) 
                        PrintToChat(player, lang.GetMessage("BuildCraftFailNotify", this, player.UserIDString));
                    item.LoseCondition(1f);
                    Card(player, card.accessLevel);
                    return null;
                }

                item.LoseCondition(1f);
                Card(player, card.accessLevel);

                cardReader.Invoke(new Action(cardReader.GrantCard), 0.5f);
                return true;
            }

            if (card.accessLevel == cardReader.accessLevel)
                Card(player, card.accessLevel);

            return null;
        }


        #endregion

        #region Build_Craft Ultimate

        void ScrapItems(Item item, LootContainer container)
        {
            if ((!config.ultimate_settings.ultimate_scavenger.scrap_skinned_items && item.skin > 0) || (!config.ultimate_settings.ultimate_scavenger.scrap_named_items && !string.IsNullOrEmpty(item.name)) || config.ultimate_settings.ultimate_scavenger.item_blacklist.Contains(item.info.shortname)) return;
            var blueprint = item.info.Blueprint;
            if (blueprint == null) return;
            item.RemoveFromContainer();
            foreach (var ingredient in blueprint.ingredients)
            {
                int amount;
                if (ingredient.itemDef.shortname == "scrap") amount = blueprint.scrapFromRecycle;
                else amount = UnityEngine.Random.Range(0, 101) <= 50 ? Mathf.CeilToInt(ingredient.amount / 2) : Mathf.FloorToInt(ingredient.amount / 2);
                if (amount == 0) continue;
                var component = ItemManager.CreateByName(ingredient.itemDef.shortname, amount * item.amount);
                container.inventory.capacity++;
                if (!component.MoveToContainer(container.inventory)) component.DropAndTossUpwards(container.transform.position);
            }
        }


        #endregion

        #region Skinning Ultimate

        public enum AnimalBuff
        { 
            Chicken, //No fall damage
            Wolf, // Healing increased for each member around you
            Boar, // Access to special loot table for certain items.
            Stag, // Every x seconds the player will get a hud message if a hostile is nearby.
            Bear, // If you attack an NPC, the NPC may not attack you back for a short period of time.
            PolarBear // Overshield
        }

        public class SkinningUltimateBuff
        {
            public AnimalBuff buff;
            public Timer _timer;
            public SkinningUltimateBuff(AnimalBuff buff)
            {
                this.buff = buff;
            }

            public void DestroyTimer()
            {
                if (_timer != null && !_timer.Destroyed) _timer.Destroy();
            }            
        }

        public Dictionary<BasePlayer, SkinningUltimateBuff> BuffedPlayers = new Dictionary<BasePlayer, SkinningUltimateBuff>();
        Dictionary<BasePlayer, float> OverShields = new Dictionary<BasePlayer, float>();

        void AddAnimalBuff(BasePlayer player, AnimalBuff animal)
        {
            if (!IsUltimateEnabled(player, Buff.Skinning_Ultimate)) return;
            BuffDetails bd;
            if (buffDetails.TryGetValue(player.userID, out bd))
            {
                if (bd.buff_values.ContainsKey(Buff.Skinning_Ultimate) && IsUltimateEnabled(player, Buff.Skinning_Ultimate))
                {
                    if (!config.ultimate_settings.ultimate_skinning.enabled_buffs.ContainsKey(animal) || config.ultimate_settings.ultimate_skinning.enabled_buffs[animal] == 0) return;
                    PrintToChat(player, GetAnimalBuffDescription(animal));

                    
                    if (BuffedPlayers.ContainsKey(player)) RemoveAnimalBuff(player);
                    SkinningUltimateBuff sub;
                    BuffedPlayers.Add(player, sub = new SkinningUltimateBuff(animal));

                    sub.DestroyTimer();

                    if (animal == AnimalBuff.Bear)
                    {
                        Subscribe("OnNpcTarget");
                        if (!CanNpcAttack_subbed.Contains(player)) CanNpcAttack_subbed.Add(player);
                    } 
                    else if (animal == AnimalBuff.PolarBear)
                    {
                        if (!OverShields.ContainsKey(player)) OverShields.Add(player, config.ultimate_settings.ultimate_skinning.bear_overshield_max);
                        else OverShields[player] = config.ultimate_settings.ultimate_skinning.bear_overshield_max;
                        Overshield_main(player, config.ultimate_settings.ultimate_skinning.bear_overshield_max);
                    }
                    else if (animal == AnimalBuff.Stag)
                    {
                        Timer _t;
                        if (StagDangerTimers.TryGetValue(player, out _t))
                        {
                            if (_t != null && !_t.Destroyed) _t.Destroy();
                            StagDangerTimers.Remove(player);
                        }
                        StagDangerTimers.Add(player, timer.Every(config.ultimate_settings.ultimate_skinning.stag_timer, () =>
                        {
                            ProcessStagTimer(player);
                        }));
                    }
                    sub._timer = timer.Once(config.ultimate_settings.ultimate_skinning.enabled_buffs[animal], () =>
                    {
                        RemoveAnimalBuff(player, true);                                             
                    });
                }
            }            
        }

        string GetAnimalBuffDescription(AnimalBuff animal)
        {
            switch (animal)
            {
                case AnimalBuff.Chicken: return "You feel the power of the chicken flow through you. You can no longer receive fall damage.";
                case AnimalBuff.Bear: return "You feel the power of the bear flow through you. Scientists cower in fear and will not engage unless engaged.";
                case AnimalBuff.Wolf: return "You feel the power of the wolf flow through you. Healing feels more potent with friends around.";
                case AnimalBuff.Boar: return "You feel the power of the boar flow through you. You may find some useful stuff when collecting wild berries and mushrooms.";
                case AnimalBuff.Stag: return "You feel the power of the stag flow through you. You feel incredibly alert to nearby player threats.";
                case AnimalBuff.PolarBear: return "You feel the power of the polar bear flow through you. It has wrapped you in a thick, damage absorbing skin.";
                default: return null;
            }
        }

        void RemoveAnimalBuff(BasePlayer player, bool timed_out = false)
        {
            SkinningUltimateBuff _buffs;
            if (BuffedPlayers.TryGetValue(player, out _buffs))
            {
                var buff = _buffs.buff;
                if (buff == AnimalBuff.PolarBear)
                {
                    OverShields.Remove(player);
                    CuiHelper.DestroyUi(player, "Overshield_main");
                }
                else if (buff == AnimalBuff.Bear)
                {
                    CanNpcAttack_subbed.Remove(player);
                    if (CanNpcAttack_subbed.Count == 0)
                    {
                        Unsubscribe("OnNpcTarget");
                    }
                }
                else if (buff == AnimalBuff.Stag)
                {
                    Timer _t;
                    if (StagDangerTimers.TryGetValue(player, out _t))
                    {
                        if (_t != null && !_t.Destroyed) _t.Destroy();
                        StagDangerTimers.Remove(player);                        
                    }
                    CuiHelper.DestroyUi(player, "StagDangerUI");
                }

                _buffs.DestroyTimer();
                BuffedPlayers.Remove(player);

                if (timed_out && player != null && player.IsAlive() && player.IsConnected) PrintToChat(player, $"You feel the power of the {buff} leave you...");
            }
        }

        Dictionary<BasePlayer, Timer> StagDangerTimers = new Dictionary<BasePlayer, Timer>();

        void ProcessStagTimer(BasePlayer player)
        {
            if (player.InSafeZone()) return;
            List<BasePlayer> neutral_players = Pool.GetList<BasePlayer>();
            neutral_players.AddRange(FindEntitiesOfType<BasePlayer>(player.transform.position, config.ultimate_settings.ultimate_skinning.stag_danger_dist).Where(x => x != player && !x.InSafeZone() && (x.Team == null || player.Team == null || x.Team.teamID != player.Team.teamID)));
            if (neutral_players.Count > 0)
            {
                StagDangerUI(player);
                if (config.ultimate_settings.ultimate_skinning.stag_draw_enemy)
                {
                    var wasAdmin = player.IsAdmin;
                    if (!wasAdmin)
                    {
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                        player.SendNetworkUpdateImmediate();
                    }

                    foreach (var threat in neutral_players)
                        player.SendConsoleCommand("ddraw.text", 10f, Color.red, threat.transform.position, "<size=10>Detection</size>");

                    if (!wasAdmin)
                    {
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                        player.SendNetworkUpdateImmediate();
                    }
                }                
            }
            else CuiHelper.DestroyUi(player, "StagDangerUI");
            Pool.FreeList(ref neutral_players);
        }

        bool HasAnimalBuff(BasePlayer player, AnimalBuff animal)
        {
            SkinningUltimateBuff sub;
            return BuffedPlayers.TryGetValue(player, out sub) && sub.buff == animal;
        }

        List<BasePlayer> CanNpcAttack_subbed = new List<BasePlayer>();

        object OnNpcTarget(ScientistNPC npc, BasePlayer player)
        {
            if (HasAnimalBuff(player, AnimalBuff.Bear))
            {
                return true;
            }
                
            return null;
        }

        private void Overshield_main(BasePlayer player, float health)
        {
            if (health <= 0) return;

            var x_value = (100 / config.ultimate_settings.ultimate_skinning.bear_overshield_max) * health;

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.3490566 0.3441171 0.3441171 0.5019608" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-201.4 87.6", OffsetMax = "-97.4 107.6" }
            }, "Hud", "Overshield_main");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.05660379 0.05660379 0.05660379 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50 -8", OffsetMax = "50 8" }
            }, "Overshield_main", "Overshield_empty");

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.2901961 0.4711963 0.5411765 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50 -8", OffsetMax = $"{-50 + x_value} 8" }
            }, "Overshield_main", "Overshield_pump");

            container.Add(new CuiElement
            {
                Name = "Label_4442",
                Parent = "Overshield_main",
                Components = {
                    new CuiTextComponent { Text = "Overshield", Font = "robotocondensed-regular.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50 -8", OffsetMax = "50 8" }
                }
            });

            CuiHelper.DestroyUi(player, "Overshield_main");
            CuiHelper.AddUi(player, container);
        }

        private void StagDangerUI(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = "StagDangerUI",
                Parent = "Hud",
                Components = {
                    new CuiTextComponent { Text = $"NEUTRAL PLAYER DETECTED NEARBY", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 0 0 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-201.4 87.6", OffsetMax = "33.4 107.6" }
                    //new CuiRectTransformComponent { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-201.4 87.6", OffsetMax = "-97.4 107.6" }
                }
            });

            CuiHelper.DestroyUi(player, "StagDangerUI");
            CuiHelper.AddUi(player, container);
        }

        void RollBoarLoot(BasePlayer player, CollectibleEntity entity)
        {
            if (!IsUltimateEnabled(player, Buff.Skinning_Ultimate)) return;
            if (UnityEngine.Random.Range(0f, 100f) <= config.ultimate_settings.ultimate_skinning.boar_chance)
            {
                PrintToChat(player, $"You find something burried under the {(entity.PrefabName.StartsWith("assets/content/nature/plants/mushroom/") ? "mushroom" : "berry bush")}...");
                List<ItemDefinition> items = Pool.GetList<ItemDefinition>();
                items.AddRange(component_item_list.Where(x => !config.ultimate_settings.ultimate_skinning.boar_blackList.Contains(x.shortname)));
                var itemDef = items.GetRandom();

                player.GiveItem(ItemManager.CreateByName(itemDef.shortname, UnityEngine.Random.Range(config.ultimate_settings.ultimate_skinning.boar_min_quantity, config.ultimate_settings.ultimate_skinning.boar_min_quantity)));
            } 
        }


        #endregion

        #region Underwater breathing behaviour

        void DestroyWaterBreathing(BasePlayer player)
        {
            BreathTime.Remove(player);
            var gameObject = player.GetComponent<WaterBreathing>();
            if (gameObject != null) GameObject.Destroy(gameObject);
            CuiHelper.DestroyUi(player, "UnderwaterBreathCounter");            
        }

        void UpdateWaterBreathing(BasePlayer player, float value)
        {
            DestroyWaterBreathing(player);
            BreathTime.Add(player, value);
            player.gameObject.AddComponent<WaterBreathing>();
            player.GetComponent<WaterBreathing>();
        }

        private static Dictionary<BasePlayer, float> BreathTime = new Dictionary<BasePlayer, float>();

        public class WaterBreathing : MonoBehaviour
        {
            private BasePlayer player;            

            private float checkDelay;

            private float max_breathing_time;
            private bool IsSwimming;
            private float time_breathing;
            private bool exceeded_breathing_time;

            // Awake() is part of the Monobehaviour class.
            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                checkDelay = Time.time + 3f;
                max_breathing_time = BreathTime[player];
            }

            // FixedUpdate() is also part of the monobehaviour class.
            public void FixedUpdate()
            {
                if (player == null) return;               

                if (checkDelay < Time.time)
                {
                    if (player.metabolism.oxygen.value < 1f)
                    {
                        if (exceeded_breathing_time) return;
                        if (!IsSwimming)
                        {
                            IsSwimming = true;                            
                        }
                        else time_breathing += 2;                        

                        player.metabolism.oxygen.SetValue(1f);

                        if (time_breathing > max_breathing_time)
                        {
                            exceeded_breathing_time = true;
                        }
                        UnderwaterBreathCounter(player, Convert.ToInt32(max_breathing_time - time_breathing));

                        checkDelay = Time.time + 2f;
                    }
                    else
                    {
                        if (IsSwimming)
                        {
                            IsSwimming = false;
                            time_breathing = 0f;
                            exceeded_breathing_time = false;
                            CuiHelper.DestroyUi(player, "UnderwaterBreathCounter");
                        }                            
                        checkDelay = Time.time + 2f;
                    }
                }
            }

            // OnDestroy() built into the monobehaviour class.
            private void OnDestroy()
            {
                enabled = false;
                CancelInvoke();
            }
        }

        private static void UnderwaterBreathCounter(BasePlayer player, float value)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1981132 0.1981132 0.1981132 0.8" },
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-48 -48", OffsetMax = "-12 -12" }
            }, "Hud", "UnderwaterBreathCounter");

            container.Add(new CuiElement
            {
                Name = "Image_2618",
                Parent = "UnderwaterBreathCounter",
                Components = {
                    new CuiImageComponent { Color = "1 1 1 1", Sprite = "assets/icons/lungs.png" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-16 -16", OffsetMax = "16 16" }
                }
            });

            container.Add(new CuiElement
            {
                Name = "Label_6078",
                Parent = "UnderwaterBreathCounter",
                Components = {
                    new CuiTextComponent { Text = value.ToString(), Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.LowerCenter, Color = "1 0.8178636 0 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-16 -16", OffsetMax = "16 16" }
                }
            });

            CuiHelper.DestroyUi(player, "UnderwaterBreathCounter");
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Instant Untie

        public class InstantUntie : MonoBehaviour
        {
            private BasePlayer player;
            private float checkDelay;
            private bool hitSuccess;

            // Awake() is part of the Monobehaviour class.
            private void Awake()
            {
                player = GetComponent<BasePlayer>();
                checkDelay = Time.time + 0.1f;
            }

            // FixedUpdate() is also part of the monobehaviour class.
            public void FixedUpdate()
            {
                if (player == null || player.WaterFactor() < 1f) return;
                if (checkDelay < Time.time)
                {
                    if (player.serverInput.IsDown(BUTTON.USE) && !hitSuccess)
                    {
                        var target = GetTargetEntity(player);
                        if (target != null && target is FreeableLootContainer)
                        {
                            var lootContainer = target as FreeableLootContainer;
                            if (!lootContainer.IsTiedDown()) return;
                            lootContainer.buoyancy.buoyancyScale = 1;
                            lootContainer.GetRB().isKinematic = false;
                            lootContainer.buoyancy.enabled = true;
                            lootContainer.SetFlag(BaseEntity.Flags.Reserved8, false);
                            lootContainer.SendNetworkUpdate();

                            hitSuccess = true;
                        }
                    }
                    if (hitSuccess) checkDelay = Time.time + 2.0f;
                    else checkDelay = Time.time + 0.1f;
                    hitSuccess = false;
                }
            }

            // OnDestroy() built into the monobehaviour class.
            private void OnDestroy()
            {
                enabled = false;
                CancelInvoke();
            }
        }

        void DestroyInstantUntie(BasePlayer player)
        {
            BreathTime.Remove(player);
            var gameObject = player.GetComponent<InstantUntie>();
            if (gameObject != null) GameObject.Destroy(gameObject);
        }

        void UpdateInstantUntie(BasePlayer player)
        {
            DestroyInstantUntie(player);
            player.gameObject.AddComponent<InstantUntie>();
        }

        private const int LAYER_TARGET = ~(1 << 2 | 1 << 3 | 1 << 4 | 1 << 10 | 1 << 18 | 1 << 28 | 1 << 29);
        private static BaseEntity GetTargetEntity(BasePlayer player)
        {
            RaycastHit raycastHit;
            bool flag = Physics.Raycast(player.eyes.HeadRay(), out raycastHit, 5, LAYER_TARGET);
            var targetEntity = flag ? raycastHit.GetEntity() : null;
            return targetEntity;
        }

        #endregion

        #region Handle API Nodes

        bool NewNodesAdded = false;
        public Dictionary<string, Dictionary<string, Configuration.TreeInfo.NodeInfo>> QueuedNodes = new Dictionary<string, Dictionary<string, Configuration.TreeInfo.NodeInfo>>();

        // AddNode(string tree, string node, bool enabled, int max_level, int tier, float value_per_buff, string buff, string buffType, string icon_url, object[] perms { string perms_description, Dictionary<int, List<string>> tiers_and_perms })
        [HookMethod("AddNode")]
        public void AddNode(string tree, string node, bool enabled, int max_Level, int tier, float value_Per_Buff, string _buff, string _buffType, string icon_url, object[] _perms = null, ulong skin = 0)
        {
            Buff buff;
            if (!Enum.TryParse(_buff, out buff) || buff == Buff.None) return;
            BuffType buffType;
            if (!Enum.TryParse(_buffType, out buffType)) return;
            if (string.IsNullOrEmpty(tree)) return;
            if (!config.trees.ContainsKey(tree)) return;
            if (string.IsNullOrEmpty(node)) return;
            if (config.trees[tree].nodes.ContainsKey(node)) return;
            if (string.IsNullOrEmpty(icon_url)) return;

            Permissions perms = null;

            if (_perms != null && _perms.Length == 2 && !string.IsNullOrEmpty((string)_perms[0]) && (Dictionary<int, Dictionary<string, string>>)_perms[1] != null)
            {
                Dictionary<int, PermissionInfo> perms_to_add = new Dictionary<int, PermissionInfo>();
                foreach (var kvp in (Dictionary<int, Dictionary<string, string>>)_perms[1])
                {
                    perms_to_add.Add(kvp.Key, new PermissionInfo(kvp.Value));
                }
                perms = new Permissions((string)_perms[0], perms_to_add);
            }
            //new Configuration.TreeInfo(new Dictionary<string, Configuration.TreeInfo.NodeInfo>()
            Puts($"Queuing new node {node}, to be added to tree: {tree} - Buff: {buff} - Type: {buffType}");
            var NodeData = new Configuration.TreeInfo.NodeInfo(enabled, max_Level, tier, value_Per_Buff, new KeyValuePair<Buff, BuffType>(buff, buffType), icon_url, skin, perms);
            if (!QueuedNodes.ContainsKey(tree)) QueuedNodes.Add(tree, new Dictionary<string, Configuration.TreeInfo.NodeInfo>());
            if (!QueuedNodes[tree].ContainsKey(node))
            {
                QueuedNodes[tree].Add(node, NodeData);
                NewNodesAdded = true;
            }
            //config.trees[tree].nodes.Add(node, new Configuration.TreeInfo.NodeInfo(enabled, max_level, tier, value_per_buff, new KeyValuePair<Buff, BuffType>(buff, buffType), icon_url, perms));            
        }

        [ChatCommand("addtestpermsnode")]
        void AddTestPermsNode(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, "skilltree.admin")) return;
            if (!config.trees.ContainsKey("Cooking")) return;
            if (config.trees["Cooking"].nodes.ContainsKey("Test perms node")) return;

            config.trees["Cooking"].nodes.Add("Test perms node", new Configuration.TreeInfo.NodeInfo(false, 2, 2, 1, new KeyValuePair<Buff, BuffType>(Buff.Permission, BuffType.Permission), "https://www.dropbox.com/s/z0taj97otweveys/ExampleImg.png?dl=1", 0, new Permissions("This is a test node. You can add your description here. Level 1 gives instant cooking. Level 2 gives free cooking.", new Dictionary<int, PermissionInfo>()
            {
                [1] = new PermissionInfo(new Dictionary<string, string>() { ["cooking.instant"] = "Instant Cooking" }),
                [2] = new PermissionInfo(new Dictionary<string, string>() { ["cooking.instant"] = "Instant Cooking", ["cooking.free"] = "Free Cooking" })
            })));

            SaveConfig();
            PrintToChat(player, "Saved new node called 'Test perms node' in the Cooking tree.");
        }

        #endregion
    }
}
