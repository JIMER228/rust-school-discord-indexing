//Requires: EventHelper
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/*
 * Add command to create crate location like in SurvivalArena.
 */

/* 1.1.18
 * Updated pooling.
 */

namespace Oxide.Plugins
{
    [Info("HungerGames", "imthenewguy", "1.1.18")]
    [Description("Hunger games")]
    class HungerGames : RustPlugin
    {
        [PluginReference]
        private Plugin EventHelper, NightVision, Economics, ServerRewards;

        #region Config

        private Configuration config;
        public class Configuration
        {
            [JsonProperty("Time after initiation before game begins (spent in the lobby) [seconds]")]
            public int Delay_Before_Start = 300;

            [JsonProperty("Delay after Hungergames starts before initiating the circle [seconds]")]
            public float Delay_Before_Circle_Start = 60;

            [JsonProperty("How often should we check if a player is outside the zone.")]
            public float rad_Delay = 3f;

            [JsonProperty("How much should the rads increase each time?")]
            public float rad_amount = 3f;

            [JsonProperty("Ring shrink percentage")]
            public float ring_shrink_dist_amount = 0.5f;

            [JsonProperty("Time after circle spawn before it starts building radiation [seconds]?")]
            public float radiation_start_delay = 20f;

            [JsonProperty("Seconds between ring shrinking events")]
            public float ring_shrink_timer = 1f;

            [JsonProperty("Outer distance")]
            public float max_distance = 230f;

            [JsonProperty("Dome darkness value")]
            public int dome_Darkness = 8;

            [JsonProperty("Minimum ring size")]
            public int minimum_ring_size = 20;

            [JsonProperty("Announce player deaths?")]
            public bool report_deaths = true;

            [JsonProperty("Heal a player when they kill another player?")]
            public bool heal_player_on_kill = true;

            [JsonProperty("How much health?")]
            public float health_on_kill = 50f;

            [JsonProperty("Minimum players to start a game?")]
            public int minimum_players = 2;

            [JsonProperty("Command to join the game.")]
            public string join_command = "hg";

            [JsonProperty("Command to leave the game.")]
            public string leave_command = "leave";
            
            [JsonProperty("How often should we run Hunger Games (set to 0 if you do not want auto start)? [seconds]")]
            public float auto_start_time = 3600;

            [JsonProperty("Despawn bodies when the game ends?")]
            public bool despawn_on_game_end = true;

            [JsonProperty("Should we teleport or kill players who are in the lobby after the game has started [true = teleport, false = kill]")]
            public bool teleport_afk = true;

            [JsonProperty("Remove players from their teams when the game starts?")]
            public bool remove_teams = false;

            [JsonProperty("How many rolls on the reward should a winner get?")]
            public int prize_quantity = 1;

            [JsonProperty("Clear data on new wipe?")]
            public bool clear_data_on_wipe = true;

            [JsonProperty("Maximum time for a game to run (seconds)")]
            public float max_run_time = 2700f;

            [JsonProperty("Broadcast when a player joins the event?")]
            public bool broadcast_join = true;

            [JsonProperty("Commands to prevent when a player is in hungergames")]
            public string[] prevent_commands = { "kit" };

            [JsonProperty("Slay modifier. Default 0.7. Change to 0.5 if players who are not in the lobby when the bubble spawns are being slain")]
            public double slay_y_modifier = 0.7;

            [JsonProperty("Remove animals that spawn on the island?")]
            public bool remove_animals = true;

            [JsonProperty("Prevent mineable resource nodes from spawning on the island")]
            public bool remove_nodes = true;

            [JsonProperty("Minimum number of loot items per container")]
            public int min_loot_amount = 3;

            [JsonProperty("Maximum number of loot items per container")]
            public int max_loot_amount = 3;

            [JsonProperty("Loot respawn delay min [seconds]")]
            public float respawn_delay_min = 60;

            [JsonProperty("Loot respawn delay max [seconds]")]
            public float respawn_delay_max = 180;

            [JsonProperty("Run the game automatically with EventHelper?")]
            public bool use_event_helper = true;

            [JsonProperty("Give items back on respawn [set to false if you are running kits on spawn]")]
            public bool give_items_back_on_spawn = true;

            [JsonProperty("Delete all backpacks after the game ends?")]
            public bool delete_all_backpacks = false;

            [JsonProperty("[Requires Economics] Award players with economic dollars for winning?")]
            public EconomicsInfo give_economics = new EconomicsInfo();

            [JsonProperty("Use the loot list from the config?")]
            public bool use_config_loot = true;

            [JsonProperty("[Requires Server Rewards] Award players with server reward points for winning?")]
            public SRPInfo give_srp = new SRPInfo();

            [JsonProperty("Commands to fire when a player wins the event [{id} {name} can be used to target the winner]")]
            public List<string> commands_on_win = new List<string>();

            [JsonProperty("Prize information")]
            public List<PrizesInfo> prizes = new List<PrizesInfo>();

            [JsonProperty("Game loot profiles")]
            public Dictionary<string, Dictionary<string, int>> loot_profiles = new Dictionary<string, Dictionary<string, int>>();

            [JsonProperty("Loot crate spawn positions")]
            public List<Vector3> loot_spawn_locations = new List<Vector3>();

            [JsonProperty("AFK Spawn locations")]
            public List<Vector3> afk_spawn_locations = new List<Vector3>();

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            config.loot_spawn_locations = default_loot_spawns;
            config.loot_profiles = default_loot_profiles;
            config.prizes = default_prizes;
            config.afk_spawn_locations = defaultAFKSpawns;
        }

        public class SRPInfo
        {
            public bool enabled = false;
            public int min_srp = 100;
            public int max_srp = 200;
        }

        public class EconomicsInfo
        {
            public bool enabled = false;
            public double min_economics = 100;
            public double max_economics = 200;
        }

        List<PrizesInfo> default_prizes
        {
            get
            {
                return new List<PrizesInfo>()
                {
                    new PrizesInfo("scrap", 0, 250, 400),
                    new PrizesInfo("wood", 0, 4000, 6000),
                    new PrizesInfo("stones", 0, 4000, 6000),
                    new PrizesInfo("sulfur", 0, 1000, 2000)
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
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintToConsole("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                PrintToConsole($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            PrintToConsole($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["hg-prefix"] = "<color=#DFF008>[Hunger Games]</color>",
                ["hg1"] = "<color=#DFF008>[Hunger Games]</color> {0} has won the event!",
                ["hg2"] = "You have won the game!\nType <color=#9CF817>/claimprize</color> to receive your reward.",
                ["hg3"] = "<color=#DFF008>[Hunger Games]</color> Everyone died!",
                ["hg4"] = "You have <color=#9CF817>{0}</color> prizes left to claim.\nType <color=#9CF817>/claimprize</color> to receive it.",
                ["hg5"] = "{0} Failed to setup. Reload the plugin and try again.",
                ["hg6"] = "{0} Will be starting shortly. The game will begin in <color=#D1FE07>{1}</color> seconds if there are enough players. Type <color=#F85CFF>/{2}</color> in chat to join. Your items will be stored until the event ends.",
                ["hg7"] = "{0} Game starting in <color=#F85CFF>{1}</color> seconds (<color=#F85CFF>{2}/{3}</color>). Type <color=#F85CFF>/hg</color> in chat to join.",
                ["hg8"] = "{0} Joining closed.",
                ["hg9"] = "Game starting in 10 seconds.",
                ["hg10"] = "{0} Hungergames cancelled due to lack of players in the event ({1}/{2}).",
                ["hg11"] = "Game starting - find weapons and be the last man standing.\nMay the odds be ever in you favor.",
                ["hg12"] = "{0} You have been slain for staying in the lobby.",
                ["hg13"] = "{0} You were removed from your team.",
                ["hg14"] = "Particinapnts:{0}",
                ["hg15"] = "Added position {0} as a potential end zone.",
                ["hg16"] = "{0} You do not have any oustanding prizes to claim.",
                ["hg17"] = "{0} You cannot use this command while an event is running.",
                ["hg18"] = "{0} Your item was dropped to the ground as there is no room in your inventory.",
                ["hg25"] = "{0} There is already an event running.",
                ["hg26"] = "{0} There are no games running.",
                ["hg27"] = "{0} You are no longer allowed to join this event.",
                ["hg28"] = "{0} Could not join event. Check print out from Inventory Manager for more info.",
                ["hg29"] = "{0} Ending Hungergames",
                ["hg30"] = "{0} <color=#27E5FC>{1}</color> joined the event. There are now (<color=#F85CFF>{2}/{3}</color>) on the roster.",
                ["hg31"] = "{0} You have been added to the Hunger Games roster. Get to an elevator quick!",
                ["hg32"] = "The command {0} is not available while playing Hunger Games.",
                ["hg33"] = "{0} You left the game.",
                ["hg34"] = "<color=#15F5E4>{0}</color> has been eliminated. Only <color=#F5AA15>{1}</color> players remain!",
                ["hg35"] = "Game has gone on too long...",
                ["hg36"] = "Circle radiation has started.",
                ["hg37"] = "The circle final circle has been created.",
                ["hg38"] = "The circle has started to close.",
                ["hg39"] = "{0} You were awarded {1}x {2} to {3}.",
                ["hg40"] = "{0} No item definition found with the shortname: {1}.",
                ["hg41"] = "{0} You have been teleport for staying in the lobby.",
                ["hg42"] = "{0} You are still in the lobby and will be {1} in {2} seconds.",
                ["hg43"] = "removed from the game",
                ["hg44"] = "teleported into the game",
                ["GivenEconomics"] = "You received <color=#fbff00>{0}</color> economic dollars.",
                ["GivenSRP"] = "You received <color=#fbff00>{0}</color> server reward points.",
                ["InRadZone"] = "You are in the radiation zone!"
            }, this);
        }

        #endregion

        #region Data

        PlayerEntity pcdData;
        private DynamicConfigFile PCDDATA;
        private TimerInfo TimerData;
        private List<BasePlayer> Participants = new List<BasePlayer>();
        public bool IsRunning = false;
        public string loot_profile;
        List<ulong> OutofBounds = new List<ulong>();
        public string Prefix;
        private bool AllowJoin;
        private List<PlayerCorpse> Corpses = new List<PlayerCorpse>();

        void Init()
        {
            PCDDATA = Interface.Oxide.DataFileSystem.GetFile("HungerGames");
            permission.RegisterPermission("hungergames.admin", this);
            permission.RegisterPermission("hungergames.list", this);
            TimerData = new TimerInfo();
            Unsubscribe("OnPlayerCorpseSpawned");
            Unsubscribe("CanEntityTakeDamage");
            Unsubscribe("OnElevatorButtonPress");
            Unsubscribe("OnPlayerDeath");
            Unsubscribe("OnPlayerMetabolize");
            Unsubscribe("OnPlayerDisconnected");
            Unsubscribe("OnPlayerCommand");
            Unsubscribe("CanRedeemKit");
            Unsubscribe("OnEntityKill");
            Unsubscribe("OnLootSpawn");
            Unsubscribe("CanPopulateLoot");
            Unsubscribe("OnContainerPopulate");
        }

        void Unload()
        {            
            destroyspheres();
            EndHungerGames();
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "BleedingPanel");
            }

            if (config.delete_all_backpacks) DeleteBackpacks();

            EventHelper.Call("EMRemoveEvent", this.Name);
            SaveData();

            cmd.RemoveChatCommand(config.join_command, this);
            cmd.RemoveChatCommand(config.leave_command, this);
            cmd.RemoveChatCommand("starthg", this);
            cmd.RemoveChatCommand("addprize", this);
            cmd.RemoveChatCommand("removeprize", this);
            cmd.RemoveChatCommand("listprizes", this);
            cmd.RemoveChatCommand("claimprize", this);
            cmd.RemoveChatCommand("endhg", this);
            cmd.RemoveChatCommand("addfinalpoint", this);
            cmd.RemoveChatCommand("hgplayers", this);
            cmd.RemoveChatCommand("resethgdata", this);
        }

        void Loaded()
        {
            LoadData();
            LoadConfig();            
        }

        void SaveData()
        {
            PCDDATA.WriteObject(pcdData);
        }

        void LoadData()
        {
            try
            {
                pcdData = Interface.Oxide.DataFileSystem.ReadObject<PlayerEntity>("HungerGames");
            }
            catch
            {
                Puts("Couldn't load player data, creating new Playerfile");
                pcdData = new PlayerEntity();
            }
        }
        class PlayerEntity
        {
            public List<Vector3> LobbyLocations = new List<Vector3>();
            public Vector3 Top_Centre_Point;
            public Dictionary<ulong, PlayerInfo> pentity = new Dictionary<ulong, PlayerInfo>();
            public Dictionary<string, ulong> ButtonIDs = new Dictionary<string, ulong>();
            public bool GameStarted;
            public List<Vector3> centre_points = new List<Vector3>();
            public Vector3 chosen_centre;
            public List<Vector3> afkSpawnLocations = new List<Vector3>();
        }

        class PlayerInfo
        {
            public int prizes = 0;
        }

        class TimerInfo
        {
            public Timer circleShrinkTimer;
            public bool circleShrinkTimerPause = false;
            public Timer circleDamageTimer;
            public Timer LobbyTimer;
            public Timer RecruitTimer;
            public Timer StartTimer;
            public Timer RosterTimer;
            public Timer AutoStartTimer;
            public Timer RadsDelayTimer;
            public Timer MaxRunTimer;
        }

        public class PrizesInfo
        {
            public string shortName;
            public ulong skinID;
            public int min_amount;
            public int max_amount;
            public string displayName;
            public PrizesInfo(string shortname, ulong skinID, int min_amount, int max_amount, string displayName = null)
            {
                this.shortName = shortname;
                this.skinID = skinID;
                this.min_amount = min_amount;
                this.max_amount = max_amount;
                this.displayName = displayName;
            }
        }

        PlayerInfo playerData;

        #endregion

        #region HungerGames IO & Vectors

        public PressButton start_button;
        public PressButton call_ele_button;

        private bool GetLobbyLocations()
        {
            if (pcdData.ButtonIDs.Count != 2) FindButtonIDs();

            if (start_button == null || call_ele_button == null) StoreButtonEntities();

            pcdData.LobbyLocations.Clear();

            var StartButtonLoc = start_button.transform.position;

            var x = StartButtonLoc.x - 6.614f;
            var y = StartButtonLoc.y - 3.6f;
            var z = StartButtonLoc.z - 2.478f;
            pcdData.LobbyLocations.Add(new Vector3(x, y, z));

            x = StartButtonLoc.x - 3.614f;
            y = StartButtonLoc.y - 3.6f;
            z = StartButtonLoc.z + 7.722f;
            pcdData.LobbyLocations.Add(new Vector3(x, y, z));

            x = StartButtonLoc.x + 1.886f;
            y = StartButtonLoc.y - 3.6f;
            z = StartButtonLoc.z + 8.222f;
            pcdData.LobbyLocations.Add(new Vector3(x, y, z));

            x = StartButtonLoc.x + 6.286f;
            y = StartButtonLoc.y - 3.6f;
            z = StartButtonLoc.z + 8.222f;
            pcdData.LobbyLocations.Add(new Vector3(x, y, z));

            x = StartButtonLoc.x + 1.744f;
            y = StartButtonLoc.y + 17.867f;
            z = StartButtonLoc.z - 0.326f;
            pcdData.Top_Centre_Point = new Vector3(x, y, z);
            if (pcdData.centre_points.Count > 0)
            {
                if (Vector3.Distance(pcdData.Top_Centre_Point, pcdData.centre_points.First()) > 100)
                {
                    pcdData.centre_points.Clear();
                    pcdData.centre_points.Add(pcdData.Top_Centre_Point);
                }
                    
            }
            SaveData();
            return true;
        }

        List<Vector3> defaultAFKSpawns
        {
            get
            {
                return new List<Vector3>()
                {
                    new Vector3(61.667f, 28.943f, -21.591f),
                    new Vector3(116.372f, 24.06f, 3.967f),
                    new Vector3(146.17f, 23.729f, 21.683f),
                    new Vector3(146.373f, 20.197f, 98.021f),
                    new Vector3(92.711f, 23.37f, 102.071f),
                    new Vector3(-30.303f, 30.53f, 111.723f),
                    new Vector3(-170.404f, 18.663f, 35.467f),
                    new Vector3(-69.87f, 21.25f, -51.549f),
                    new Vector3(28.151f, 21.481f, -124.357f),
                    new Vector3(55.763f, 26.601f, -153.413f),
                    new Vector3(121.507f, 18.978f, -54.501f)
                };
            }
        }

        private void FindButtonIDs()
        {
            var found = 0;
            foreach (var pookie in BaseNetworkable.serverEntities)
            {
                if (!pookie.ShortPrefabName.Contains("pookie")) continue;
                foreach (var entity in BaseNetworkable.serverEntities)
                {
                    var button = entity as PressButton;
                    if (button == null) continue;
                    var dist = Vector3.Distance(pookie.transform.position, button.transform.position);
                    if (dist <= 0.80)
                    {
                        start_button = button as PressButton;
                        pcdData.ButtonIDs.Add("start", button.net.ID.Value);
                        SaveData();
                        found++;
                        Puts($"Found start button at: {button.transform.position}");
                        continue;
                    }

                    if (dist >= 1.20 && dist <= 2.6)
                    {
                        call_ele_button = button as PressButton;
                        if (!pcdData.ButtonIDs.ContainsKey("ele")) pcdData.ButtonIDs.Add("ele", button.net.ID.Value);
                        else pcdData.ButtonIDs["ele"] = button.net.ID.Value;
                        SaveData();
                        found++;
                        Puts($"Found ele button at: {button.transform.position}");
                    }
                }
                if (found == 2)
                {
                    Puts($"Successfully found and stored both buttons.");
                    return;
                }
            }
            Puts("Failed to find IO entities");
        }

        private void StoreButtonEntities()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var button = entity as PressButton;
                if (button == false) continue;
                if (button.net.ID.Value == pcdData.ButtonIDs["start"])
                {
                    start_button = button;
                    continue;
                }
                if (button.net.ID.Value == pcdData.ButtonIDs["ele"])
                {
                    call_ele_button = button;
                    continue;
                }
            }
            if (start_button == null || call_ele_button == null)
            {
                LoadError();
            }
        }
        #endregion        

        #region Hooks

        void OnEntitySpawned(BaseAnimalNPC entity)
        {
            if (!config.remove_animals) return;
            if (Vector3.Distance(pcdData.Top_Centre_Point, entity.transform.position) < config.max_distance)
            {
                timer.Once(0.1f, () =>
                {
                    if (entity != null) entity.KillMessage();
                });
            }
        }

        void OnEntitySpawned(ResourceEntity entity)
        {
            if (!config.remove_nodes) return;
            if ((entity.resourceDispenser.gatherType == ResourceDispenser.GatherType.Ore) && Vector3.Distance(pcdData.Top_Centre_Point, entity.transform.position) < config.max_distance)
            {
                NextTick(() =>
                {
                    if (entity != null) entity.KillMessage();
                });
            }                
        }

        void OnPlayerCorpseSpawned(BasePlayer player, PlayerCorpse corpse)
        {
            if (!IsRunning) return;
            if (Vector3.Distance(corpse.transform.position, pcdData.chosen_centre) < config.max_distance)
            {
                Corpses.Add(corpse);
            }
        }

        object OnElevatorButtonPress(ElevatorLift lift, BasePlayer player, Elevator.Direction direction, bool toTopOrBottom)
        {
            if (pcdData.LobbyLocations.Count == 0) return null;
            if (Vector3.Distance(lift.transform.position, pcdData.LobbyLocations.First()) < 25) return false;
            return null;
        }

        [ChatCommand("lootpos")]
        void CheckLootPositions(BasePlayer player)
        {
            var loot_positions = ConvertLocalsToWorld(config.loot_spawn_locations);
            for (int i = 0; i < loot_positions.Count; i++)
            {
                player.SendConsoleCommand("ddraw.text", 10f, Color.yellow, loot_positions[i], $"Loot[{i}]");
            }
        }

        void OnServerInitialized(bool initial)
        {
            Prefix = lang.GetMessage("hg-prefix", this);
            cmd.AddChatCommand(config.join_command, this, "JoinEvent");
            cmd.AddChatCommand(config.leave_command, this, "LeaveCMD");
            cmd.AddChatCommand("starthg", this, "StartHungerGames");
            cmd.AddChatCommand("addprize", this, "AddPrize");
            cmd.AddChatCommand("removeprize", this, "RemovePrize");
            cmd.AddChatCommand("listprizes", this, "PrintPrizeToConsole");
            cmd.AddChatCommand("claimprize", this, "ClaimPrize");
            cmd.AddChatCommand("endhg", this, "EndHG");
            cmd.AddChatCommand("addfinalpoint", this, "AddEndPoint");
            cmd.AddChatCommand("hgplayers", this, "ListParticipants");
            cmd.AddChatCommand("resethgdata", this, "ResetData");

            if (!config.remove_animals && !config.remove_nodes) Unsubscribe("OnEntitySpawned");
            var saveConfig = false;
            if (config.afk_spawn_locations.Count == 0)
            {
                config.afk_spawn_locations = defaultAFKSpawns;
                saveConfig = true;
            }                

            allowRads = false;
            CheckAndSetup();

            if (start_button == null || call_ele_button == null)
            {
                Puts("Failed to get button entities. Unloading...");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }
            
            if (config.loot_spawn_locations == null || config.loot_spawn_locations.Count == 0)
            {
                config.loot_spawn_locations = default_loot_spawns;
                saveConfig = true;
            }

            loot_positions = ConvertLocalsToWorld(config.loot_spawn_locations);
            if (loot_positions.Count == 0)
            {
                Puts("Failed to get world positions for our crates. Unloading...");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }
            var boxes = BaseNetworkable.serverEntities.Where(x => x.ShortPrefabName == "crate_normal_2" && Vector3.Distance(x.transform.position, start_button.transform.position) < config.max_distance).ToList();
            foreach (var box in boxes.ToList())
            {
                box.KillMessage();
            }
            if (config.loot_profiles.Count == 0)
            {
                config.loot_profiles = default_loot_profiles;
                saveConfig = true;
            }
            if (saveConfig) SaveConfig();
            EventHelper.Call("EMCreateEvent", this.Name, config.use_event_helper, true, true, true, config.give_items_back_on_spawn, true, pcdData.LobbyLocations.First());
            EventHelper.Call("EMExternalPluginSettings", this.Name);
            EventHelper.Call("EMBlackListCommands", this.Name, config.prevent_commands);
            if (!config.use_event_helper)
            {
                if (config.auto_start_time > 0) TimerData.AutoStartTimer = timer.Every(config.auto_start_time, () =>
                {
                    Puts("Auto starting Hunger Games");
                    InitiateGame();
                });
            }
            if (config.prizes == null || config.prizes.Count == 0)
            {
                config.prizes = default_prizes;
                Puts("Updating prizes");
                SaveConfig();
            }
        }

        List<Vector3> loot_positions = new List<Vector3>();

        void CheckAndSetup()
        {
            if (pcdData == null || pcdData.LobbyLocations.Count == 0 || pcdData.Top_Centre_Point == null || pcdData.ButtonIDs.Count != 2 || start_button == null || call_ele_button == null) GetLobbyLocations();
            if (start_button == null || call_ele_button == null)
            {
                StoreButtonEntities();
            }
            if (call_ele_button == null)
            {
                LoadError();
                return;
            }
            if (pcdData.afkSpawnLocations.Count == 0)
            {
                GetAFKSpawnLocations();
            }
            call_ele_button.Press();
            if (pcdData.centre_points.Count == 0)
            {
                pcdData.centre_points.Add(pcdData.Top_Centre_Point);
                pcdData.chosen_centre = pcdData.centre_points.First();
                SaveData();
            }
            if (config.remove_animals || config.remove_nodes)
            {
                foreach (var entity in BaseNetworkable.serverEntities)
                {
                    if (entity is BaseAnimalNPC && config.remove_animals && Vector3.Distance(pcdData.Top_Centre_Point, entity.transform.position) < config.max_distance) NextTick(() => entity.KillMessage());
                    if (entity is ResourceEntity && config.remove_nodes && (entity as ResourceEntity).resourceDispenser.gatherType == ResourceDispenser.GatherType.Ore && Vector3.Distance(pcdData.Top_Centre_Point, entity.transform.position) < config.max_distance) NextTick(() => entity.KillMessage());
                }
            }
        }

        void GetAFKSpawnLocations()
        {
            if (config.afk_spawn_locations == null || config.afk_spawn_locations.Count == 0)
            {
                config.afk_spawn_locations = defaultAFKSpawns;
                SaveConfig();
            }
            foreach (var spawn in config.afk_spawn_locations)
            {
                pcdData.afkSpawnLocations.Add(ConvertLocalsToWorld(spawn));
            }
            SaveData();
        }

        Vector3 ConvertLocalsToWorld(BaseNetworkable anchor_entity, Vector3 loc)
        {
            var pos = anchor_entity.transform.localToWorldMatrix.MultiplyPoint3x4(loc);
            return pos;
        }

        void ResetData(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, "hungergames.admin")) return;
            pcdData.LobbyLocations.Clear();
            pcdData.GameStarted = false;
            pcdData.ButtonIDs.Clear();
            SaveData();
            CheckAndSetup();
        }

        void CheckWin()
        {
            if (!pcdData.GameStarted || !IsRunning) return;
            if (Participants.Count == 1)
            {
                var winner = Participants.First();
                PrintToChat(string.Format(lang.GetMessage("hg1", this), winner.displayName));
                PrintToChat(winner, lang.GetMessage("hg2", this, winner.UserIDString));
                LogToFile("GameRecords", $"[{DateTime.Now}] Winner: {winner.displayName}", this, true);
                if (!pcdData.pentity.ContainsKey(winner.userID)) pcdData.pentity.Add(winner.userID, new PlayerInfo());
                pcdData.pentity[winner.userID].prizes++;
                SaveData();
                winner.metabolism.radiation_level.value = winner.metabolism.radiation_level.min;
                winner.metabolism.radiation_poison.value = winner.metabolism.radiation_poison.min;
                Interface.CallHook("HGWinner", winner);

                if (!config.commands_on_win.IsNullOrEmpty())
                {
                    foreach (var command in config.commands_on_win)
                    {
                        var fixedString = command.Replace("{id}", winner.UserIDString);
                        fixedString = fixedString.Replace("{name}", winner.displayName);

                        var allArgs = fixedString.Split(' ');
                        var c = allArgs[0];
                        string[] args = null;
                        if (allArgs.Length > 1)
                            args = allArgs.Skip(1).ToArray();

                        Server.Command(allArgs[0], args);
                    }
                }

                timer.Once(5f, () =>
                {
                    EndHungerGames();
                });
            }
            if (IsRunning)
            {
                if (Participants.Count == 0)
                {
                    PrintToChat(lang.GetMessage("hg3", this));
                    EndHungerGames();
                }
            }
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (!Participants.Contains(player)) return;
            CuiHelper.DestroyUi(player, "BleedingPanel");
            if (NightVision != null)
            {
                NightVision.Call("UnlockPlayerTime", player);
            }
            if (config.heal_player_on_kill && info != null)
            {
                var attacker = info.InitiatorPlayer;
                if (attacker != null)
                {
                    if (Participants.Contains(attacker) && attacker.IsConnected) attacker.health = (attacker.health + config.health_on_kill);
                }
            }
            Participants.Remove(player);
            if (config.report_deaths)
            {
                MessageParticipants(string.Format(lang.GetMessage("hg34", this), player.displayName, Participants.Count));
            }

            CheckWin();
            return;
        }

        void HandleBan(BasePlayer player)
        {
            if (player.IsConnected) player.Kick("Removed from the event for dying.");
            ServerUsers.Set(player.userID, ServerUsers.UserGroup.Banned, string.Empty, string.Empty);
            ServerUsers.Save();
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "BleedingPanel");
            if (Convert.ToBoolean(EventHelper.Call("EMIsParticipating", player, this.Name)))
            {
                EventHelper.Call("EMPlayerLeaveEvent", player, this.Name);
            }
            if (pcdData.pentity.TryGetValue(player.userID, out playerData))
            {
                if (playerData.prizes > 0) PrintToChat(player, string.Format(lang.GetMessage("hg4", this, player.UserIDString), playerData.prizes));
                else pcdData.pentity.Remove(player.userID);
            }            
        }

        void OnPlayerMetabolize(PlayerMetabolism metabolism, BaseCombatEntity entity, float delta)
        {
            if (Participants.Count == 0) return;
            var player = entity as BasePlayer;
            if (player == null) return;

            if (Participants.Contains(player))
            {
                if (player.IsDead()) return;
                if (PlayerOutOfCircle(player))
                {
                    if (!OutofBounds.Contains(player.userID))
                    {
                        CuiHelper.DestroyUi(player, "BleedingPanel");
                        BleedingPanel(player);
                        OutofBounds.Add(player.userID);
                    }
                }
                else
                {
                    if (OutofBounds.Contains(player.userID))
                    {
                        CuiHelper.DestroyUi(player, "BleedingPanel");
                        OutofBounds.Remove(player.userID);
                    }
                }

            }
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (Participants.Contains(player))
            {
                player.Hurt(500);
            }
        }

        void OnNewSave(string filename)
        {
            pcdData.LobbyLocations.Clear();
            pcdData.GameStarted = false;
            pcdData.ButtonIDs.Clear();
            pcdData.afkSpawnLocations.Clear();
            if (config.clear_data_on_wipe) pcdData.pentity.Clear();
            SaveData();
        }

        #endregion

        #region GamePlay

        public float CircleDist;
        public bool allowRads = false;
        public bool CircleSpawned = false;

        private void InitiateCircleTimers()
        {
            CircleSpawned = true;
            TimerData.MaxRunTimer = timer.Once(config.max_run_time, () =>
            {
                MessageParticipants(lang.GetMessage("hg35", this));
                EndHungerGames();
            });
            TimerData.RadsDelayTimer = timer.Once(config.radiation_start_delay, () =>
            {
                allowRads = true;
                MessageParticipants(lang.GetMessage("hg36", this));
            });
            TimerData.circleDamageTimer = timer.Every(config.rad_Delay, () =>
            {
                if (allowRads)
                {
                    foreach (var player in Participants)
                    {
                        if (!PlayerOutOfCircle(player)) continue;
                        IncreaseRadiation(player);
                    }
                }
            });
            CreateSphere(pcdData.chosen_centre, CircleDist, config.dome_Darkness, config.ring_shrink_dist_amount);
            TimerData.circleShrinkTimer = timer.Every(config.ring_shrink_timer, () =>
            {
                if (TimerData.circleShrinkTimerPause) return;
                var shrinkDist = CircleDist - config.ring_shrink_dist_amount;
                if ((shrinkDist) > config.minimum_ring_size)
                {
                    CircleDist = shrinkDist;
                }
                else
                {
                    TimerData.circleShrinkTimerPause = true;
                    CircleDist = config.minimum_ring_size;
                    destroyspheres();
                    CreateSphere(pcdData.chosen_centre, CircleDist, config.dome_Darkness, 0f);
                    MessageParticipants(lang.GetMessage("hg37", this));
                }
            });
        }

        private void InitiateGame(int TimeOverride = 0, string profile = null)
        {
            if (!IsIOSetup())
            {
                PrintToChat(string.Format(lang.GetMessage("hg5", this), Prefix));
                return;
            }
            if (IsRunning)
            {
                Puts("Attempted to start a game but it is already running.");
                return;
            }
            LogToFile("GameRecords", $"[{DateTime.Now}] Attempting to start a new game", this, true);
            var waitTime = 0;
            if (TimeOverride > 0)
            {
                waitTime = TimeOverride;
                if (config.auto_start_time > 0)
                {
                    if (TimerData.AutoStartTimer != null && !TimerData.AutoStartTimer.Destroyed) TimerData.AutoStartTimer.Destroy();
                    if (config.auto_start_time > 0) TimerData.AutoStartTimer = timer.Every(config.auto_start_time, () =>
                    {
                        Puts("Auto starting Hunger Games");
                        InitiateGame();
                    });
                }                
            }                
            else waitTime = config.Delay_Before_Start;
            Subscribe("OnPlayerDisconnected");
            Subscribe("OnElevatorButtonPress");
            Subscribe("OnPlayerDeath");
            Subscribe("OnPlayerCorpseSpawned");
            Subscribe("OnPlayerCommand");
            Subscribe("CanRedeemKit");            
            Subscribe("OnContainerPopulate");
            EventHelper.Call("EMStartEvent", this.Name);
            AllowJoin = true;
            IsRunning = true;
            loot_profile = config.loot_profiles.Keys.ToList().GetRandom();
            call_ele_button.Press();
            pcdData.chosen_centre = pcdData.centre_points.GetRandom();
            PrintToChat(string.Format(lang.GetMessage("hg6", this), Prefix, waitTime, config.join_command));
            var time = 0;
            CircleDist = config.max_distance;
            TimerData.RecruitTimer = timer.Repeat(1f, waitTime, () =>
            {
                time++;
                
                if (time == waitTime - 120) PrintToChat(string.Format(lang.GetMessage("hg7", this), Prefix, 120, Participants.Count, config.minimum_players));
                if (time == waitTime - 60) PrintToChat(string.Format(lang.GetMessage("hg7", this), Prefix, 60, Participants.Count, config.minimum_players));
                if (time == waitTime - 30) PrintToChat(string.Format(lang.GetMessage("hg7", this), Prefix, 30, Participants.Count, config.minimum_players));
                if (time == waitTime - 10)
                {
                    PrintToChat(string.Format(lang.GetMessage("hg8", this), Prefix));
                    MessageParticipants(lang.GetMessage("hg9", this));
                    AllowJoin = false;
                }                    
                //RosterPlayers();
            });
            TimerData.LobbyTimer = timer.Once(waitTime + 1, () =>
            {
                if (Participants.Count < config.minimum_players)
                {
                    
                    PrintToChat(string.Format(lang.GetMessage("hg10", this), Prefix, Participants.Count, config.minimum_players));
                    LogToFile("GameRecords", $"[{DateTime.Now}] We do not have enough players to continue ({Participants.Count}/{config.minimum_players}).", this, true);
                    pcdData.GameStarted = false;
                    EndHungerGames();
                    return;
                }
                Subscribe("OnEntityKill");
                Subscribe("OnLootSpawn");
                Subscribe("CanPopulateLoot");
                AddCrates();
                MessageParticipants(lang.GetMessage("hg11", this));
                AllowJoin = false;
                pcdData.GameStarted = true;
                LogToFile("GameRecords", $"[{DateTime.Now}] We have enough players to continue.", this, true);
                StartElevators();
                Subscribe("CanEntityTakeDamage");                
                Subscribe("OnPlayerMetabolize");                
            });
        }

        void KillAFKs()
        {
            List<BasePlayer> killList = Pool.Get<List<BasePlayer>>();
            foreach (var player in Participants)
            {
                if (player.transform.position.y < (pcdData.Top_Centre_Point.y * config.slay_y_modifier) && player.IsAlive()) killList.Add(player);
            }

            foreach (var player in killList)
            {
                if (config.teleport_afk)
                {
                    if (player.IsConnected) PrintToChat(player, string.Format(lang.GetMessage("hg41", this, player.UserIDString), Prefix));
                    Player.Teleport(player, pcdData.afkSpawnLocations.GetRandom());
                }
                else
                {
                    if (player.IsConnected) PrintToChat(player, string.Format(lang.GetMessage("hg12", this, player.UserIDString), Prefix));
                    player.Die();
                }
            }
            Pool.FreeUnmanaged(ref killList);
        }

        void RemoveTeam()
        {
            if (!config.remove_teams) return;
            foreach (var player in Participants)
            {
                if (player.currentTeam != 0UL)
                {
                    RelationshipManager.PlayerTeam current = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                    current.RemovePlayer(player.userID);
                    PrintToChat(player, string.Format(lang.GetMessage("hg13", this, player.UserIDString), Prefix));
                    //PrintToChat(player, $"{Prefix} You were removed from your team.");
                }
            }
        }

        private void StartElevators()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (Participants.Contains(player)) continue;
                if (IsPlayerAtHungerGames(player)) Participants.Add(player);
            }

            start_button.Press();
            timer.Once(20f, () =>
            {
                foreach (var player in Participants)
                {
                    if (player.IsAlive() && player.IsConnected && player.transform.position.y < (pcdData.Top_Centre_Point.y * config.slay_y_modifier))
                    {
                        PrintToChat(player, string.Format(lang.GetMessage("hg42", this, player.UserIDString), Prefix, config.teleport_afk ? lang.GetMessage("hg44", this, player.UserIDString) : lang.GetMessage("hg43", this, player.UserIDString), config.Delay_Before_Circle_Start));
                    }
                }
            });
            TimerData.StartTimer = timer.Once(20 + config.Delay_Before_Circle_Start, () =>
            {
                InitiateCircleTimers();
                MessageParticipants(lang.GetMessage("hg38", this));
                timer.Repeat(5f, 5, () =>
                {
                    KillAFKs();
                });
                RemoveTeam();
            });
        }

        #endregion

        #region Chat Commands

        void ListParticipants(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, "hungergames.list")) return;
            if (!isGameRunning()) return;
            List<string> names = new List<string>();
            if (Participants.Count == 0) return;
            foreach (var contestant in Participants)
            {
                names.Add(contestant.displayName);
            }
            PrintToChat(player, string.Format(lang.GetMessage("hg14", this, player.UserIDString), String.Join("\n- ", names)));
        }

        void AddEndPoint(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, "hungergames.admin")) return;
            pcdData.centre_points.Add(player.transform.position);
            SaveData();
            PrintToChat(player, string.Format(lang.GetMessage("hg15", this, player.UserIDString), player.transform.position));
        }

        private void ClaimPrize(BasePlayer player, string command, string[] args)
        {
            if (!pcdData.pentity.TryGetValue(player.userID, out playerData) || playerData.prizes < 1)
            {
                PrintToChat(player, string.Format(lang.GetMessage("hg16", this, player.UserIDString), Prefix));
                return;
            }
            if (IsRunning)
            {
                PrintToChat(player, string.Format(lang.GetMessage("hg17", this, player.UserIDString), Prefix));
                return;
            }
            for (int i = 0; i < config.prize_quantity; i++)
            {
                var prize = config.prizes.GetRandom();
                var item = ItemManager.CreateByName(prize.shortName, UnityEngine.Random.Range(prize.min_amount, prize.max_amount + 1), prize.skinID);
                if (item == null) continue;
                if (prize.displayName != null) item.name = prize.displayName;
                PrintToChat(player, string.Format(lang.GetMessage("hg39", this, player.UserIDString), Prefix, item.amount, item.name ?? item.info.displayName.english, player.displayName));
                LogToFile("ClaimedPrizes", $"[{DateTime.Now}] Awarded {item.amount}x {item.info.displayName.english}", this, true);
                if (!player.inventory.containerMain.IsFull() || player.inventory.containerBelt.IsFull())
                {
                    player.GiveItem(item);
                }
                else
                {
                    item.DropAndTossUpwards(player.transform.position);
                    PrintToChat(player, string.Format(lang.GetMessage("hg18", this, player.UserIDString), Prefix));
                }
            }
            if (Economics != null && config.give_economics.enabled)
            {
                System.Random random = new System.Random();
                var amount = random.NextDouble() * (config.give_economics.min_economics - config.give_economics.max_economics) + config.give_economics.min_economics;
                if (Convert.ToBoolean(Economics.Call("Deposit", player.UserIDString, amount))) PrintToChat(player, string.Format(lang.GetMessage("GivenEconomics", this, player.UserIDString), Math.Round(amount, 2)));
            }
            if (ServerRewards != null && config.give_srp.enabled)
            {
                var amount = UnityEngine.Random.Range(config.give_srp.min_srp, config.give_srp.max_srp);
                if (Convert.ToBoolean(ServerRewards.Call("AddPoints", player.userID, amount))) PrintToChat(player, string.Format(lang.GetMessage("GivenSRP", this, player.UserIDString), amount));
            }
            if (playerData.prizes == 1) pcdData.pentity.Remove(player.userID);
            else playerData.prizes--;
            
            SaveData();
        }
        private void StartHungerGames(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "hungergames.admin")) return;
            if (IsRunning)
            {
                PrintToChat(player, string.Format(lang.GetMessage("hg25", this, player.UserIDString), Prefix));
                return;
            }

            if (config.use_event_helper)
            {
                EventHelper.Call("EMManuallyStarted", this.Name);
            }

            if (args.Length == 0) InitiateGame();
            else
            {
                if (!args[0].IsNumeric()) InitiateGame();
                else InitiateGame(Convert.ToInt32(args[0]));
            }
        }

        [ConsoleCommand("starthg")]
        void ConsoleStartHG(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon) return;
            if (config.use_event_helper)
            {
                EventHelper.Call("EMManuallyStarted", this.Name);
            }
            if (arg.Args.Length == 0) InitiateGame();
            else
            {
                if (!arg.Args[0].IsNumeric())
                {
                    foreach (var profile in config.loot_profiles)
                    {
                        if (arg.Args[0].Equals(profile.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            InitiateGame(0, profile.Key);
                            return;
                        }
                    }
                    InitiateGame();
                }
                else
                {
                    var time = Convert.ToInt32(arg.Args[0]);
                    if (arg.Args.Length == 2)
                    {
                        foreach (var profile in config.loot_profiles)
                        {
                            if (arg.Args[1].Equals(profile.Key, StringComparison.OrdinalIgnoreCase))
                            {
                                Puts($"Initiating game. Time: {time}. Profile: {profile.Key}");
                                InitiateGame(time, profile.Key);
                                return;
                            }
                        }
                    }
                    Puts($"Initating game in {time} seconds");
                    InitiateGame(time);
                }                    
            }
        }

        private void JoinEvent(BasePlayer player)
        {
            if (!IsRunning)
            {
                PrintToChat(player, string.Format(lang.GetMessage("hg26", this, player.UserIDString), Prefix));
                return;
            }
            if (!AllowJoin)
            {
                PrintToChat(player, string.Format(lang.GetMessage("hg27", this, player.UserIDString), Prefix));
                return;
            }
            if (!Convert.ToBoolean(EventHelper.Call("EMEnrollPlayer", player, this.Name)))
            {
                PrintToChat(player, "Could not join event. Check print out from EventHelper for more info.");
                return;
            }
            if (NightVision != null)
            {
                NightVision.Call("LockPlayerTime", player, 10f);
            }

            Participants.Add(player);
            if (config.broadcast_join) PrintToChat(string.Format(lang.GetMessage("hg30", this, player.UserIDString), Prefix, player.displayName, Participants.Count, config.minimum_players));
        }

        private void LeaveCMD(BasePlayer player)
        {
            LeaveEvent(player);
            EventHelper.Call("EMPlayerLeaveEvent", player, this.Name, true);
        }

        private void LeaveEvent(BasePlayer player)
        {
            if (Participants.Contains(player)) Participants.Remove(player);
            CuiHelper.DestroyUi(player, "BleedingPanel");
            if (NightVision != null)
            {
                NightVision.Call("UnlockPlayerTime", player);
            }
            player.metabolism.radiation_level.SetValue(0);
            player.metabolism.radiation_poison.SetValue(0);
            CheckWin();
        }

        #endregion

        #region Helpers
        List<BaseEntity> Spheres = new List<BaseEntity>();

        void LoadError()
        {
            pcdData.LobbyLocations.Clear();
            pcdData.GameStarted = false;
            pcdData.ButtonIDs.Clear();
            SaveData();
            Puts("Error obtaining I/O entities. Please reload the plugin.");
            Interface.Oxide.UnloadPlugin(Name);
        }

        private Vector3 RandomLocation()
        {
            if (pcdData.LobbyLocations.Count == 1) return pcdData.LobbyLocations.First();
            return pcdData.LobbyLocations.GetRandom();
        }

        bool IsIOSetup()
        {
            if (pcdData.LobbyLocations.Count == 0 || pcdData.Top_Centre_Point == null || start_button == null || call_ele_button == null)
            {
                if (!GetLobbyLocations()) return false;
            }
            return true;
        }

        private void CreateSphere(Vector3 position, float radius, int darkness, float speed)
        {
            for (int i = 0; i < darkness; i++)
            {
                SphereEntity sphere = GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", position, new Quaternion(), true) as SphereEntity;
                sphere.currentRadius = radius * 2;
                sphere.lerpSpeed = speed * 2;
                sphere.Spawn();
                Spheres.Add(sphere);
            }
        }

        private void destroyspheres()
        {
            foreach (var sphere in Spheres)
            {
                if (sphere != null)
                    sphere.KillMessage();
            }
            Spheres.Clear();
        }

        private bool PlayerOutOfCircle(BasePlayer player)
        {
            if (!CircleSpawned) return false;
            return (Vector3.Distance(player.transform.position, pcdData.chosen_centre) > CircleDist);
        }

        private void IncreaseRadiation(BasePlayer player)
        {
            player.metabolism.radiation_level.value = (player.metabolism.radiation_level.value + config.rad_amount);
            player.metabolism.radiation_poison.value = (player.metabolism.radiation_poison.value + config.rad_amount);
        }

        private void MessageParticipants(string s)
        {
            foreach (var player in Participants)
            {
                PrintToChat(player, $"{Prefix} {s}");
            }
        }

        private void EndHG(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, "hungergames.admin")) return;
            PrintToChat(player, string.Format(lang.GetMessage("hg29", this, player.UserIDString), Prefix));
            EndHungerGames();
        }
        
        private void EndHungerGames()
        {            
            allowRads = false;
            CircleSpawned = false;
            if (loot_refill_timer != null && !loot_refill_timer.Destroyed) loot_refill_timer.Destroy();
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "BleedingPanel");
            }
            foreach (var player in Participants)
            {
                if (NightVision != null)
                {
                    NightVision.Call("UnlockPlayerTime", player);
                }
                if (player.IsAlive() && player.IsConnected)
                {
                    EventHelper.Call("EMPlayerLeaveEvent", player, this.Name);
                }
            }
            if (config.despawn_on_game_end)
            {
                foreach (var corpse in Corpses)
                {
                    corpse.Kill();
                }
            }            
            Corpses.Clear();

            DeleteBackpacks();

            if (IsRunning)
            {
                IsRunning = false;
                EventHelper.Call("EMEndEvent", this.Name);
                PrintToChat( string.Format(lang.GetMessage("hg29", this), Prefix));
                DeleteBackpacks();
            }
            
            loot_profile = null;
            pcdData.GameStarted = false;
            
            if (TimerData.circleDamageTimer != null)
            {
                if (!TimerData.circleDamageTimer.Destroyed) TimerData.circleDamageTimer.Destroy();
            }
            if (TimerData.circleShrinkTimer != null)
            {
                if (!TimerData.circleShrinkTimer.Destroyed) TimerData.circleShrinkTimer.Destroy();
            }
            if (TimerData.LobbyTimer != null)
            {
                if (!TimerData.LobbyTimer.Destroyed) TimerData.LobbyTimer.Destroy();
            }
            if (TimerData.StartTimer != null)
            {
                if (!TimerData.StartTimer.Destroyed) TimerData.StartTimer.Destroy();
            }
            if (TimerData.RosterTimer != null)
            {
                if (!TimerData.RosterTimer.Destroyed) TimerData.RosterTimer.Destroy();
            }
            if (TimerData.RecruitTimer != null)
            {
                if (!TimerData.RecruitTimer.Destroyed) TimerData.RecruitTimer.Destroy();
            }
            if (TimerData.RadsDelayTimer != null)
            {
                if (!TimerData.RadsDelayTimer.Destroyed) TimerData.RadsDelayTimer.Destroy();
            }
            if (TimerData.MaxRunTimer != null)
            {
                if (!TimerData.MaxRunTimer.Destroyed) TimerData.MaxRunTimer.Destroy();
            }
            TimerData.circleShrinkTimerPause = false;
            destroyspheres();

            Participants.Clear();

            ClearCrates();

            Puts("Hungergames has ended");

            Unsubscribe("OnPlayerCorpseSpawned");
            Unsubscribe("CanEntityTakeDamage");
            Unsubscribe("OnElevatorButtonPress");
            Unsubscribe("OnPlayerDeath");
            Unsubscribe("OnPlayerMetabolize");
            Unsubscribe("OnPlayerDisconnected");
            Unsubscribe("OnPlayerCommand");
            Unsubscribe("CanRedeemKit");
            Unsubscribe("OnEntityKill");
            Unsubscribe("OnLootSpawn");
            Unsubscribe("OnContainerPopulate");
        }

        void DeleteBackpacks()
        {
            if (config.delete_all_backpacks)
            {
                List<BaseNetworkable> backpacks = Pool.Get<List<BaseNetworkable>>();
                backpacks.AddRange(BaseNetworkable.serverEntities.Where(x => x.ShortPrefabName == "item_drop_backpack" && Vector3.Distance(x.transform.position, pcdData.Top_Centre_Point) < config.max_distance));
                foreach (var backpack in backpacks.ToList())
                {
                    backpack.KillMessage();
                }
                Pool.FreeUnmanaged(ref backpacks);
            }
        }

        private void RosterPlayers()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (Participants.Contains(player))
                {
                    CuiHelper.DestroyUi(player, "BleedingPanel");
                    continue;
                }                    
                if (player.IsDead()) continue;
                if (!player.IsConnected) continue;
                if (IsPlayerAtHungerGames(player))
                {
                    Participants.Add(player);
                    if (config.broadcast_join) PrintToChat(string.Format(lang.GetMessage("hg30", this, player.UserIDString), Prefix, player.displayName, Participants.Count, config.minimum_players));
                }
                    
            }
        }

        private bool IsPlayerAtHungerGames(BasePlayer player)
        {
            if (Vector3.Distance(pcdData.LobbyLocations.First(), player.transform.position) < 35 && player.IsConnected && player.IsAlive())
            {
                PrintToChat(player, string.Format(lang.GetMessage("hg31", this, player.UserIDString), Prefix));
                //PrintToChat(player, $"{Prefix} You have been added to the Hunger Games roster. Get to an elevator quick!");
                return true;
            }
            return false;
        }

        #endregion

        #region HUDs
        void BleedingPanel(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.6415094 0.245105 0.3079055 1", Sprite = "assets/content/ui/UI.Background.Transparent.Radial.psd" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0.004" }
            }, "Overlay", "BleedingPanel");

            container.Add(new CuiElement
            {
                Name = "BleedingOutText",
                Parent = "BleedingPanel",
                FadeOut = 1,
                Components = {
                    new CuiTextComponent { Text = lang.GetMessage("InRadZone", this, player.UserIDString), Font = "robotocondensed-bold.ttf", FontSize = 50, Align = TextAnchor.MiddleCenter, Color = "1 0.7532371 0 1", FadeIn = 1 },
                    new CuiOutlineComponent { Color = "0 0 0 1", Distance = "1 1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-192.131 21.616", OffsetMax = "192.131 171.984" }
                }
            });

            CuiHelper.DestroyUi(player, "BleedingPanel");
            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region TruePVE

        object CanEntityTakeDamage(BasePlayer player, HitInfo hitinfo)
        {
            if (player == null || hitinfo == null) return null;
            if (player.net?.connection?.authLevel >= 0 && player.userID.IsSteamId())
            {
                if (Participants.Contains(player)) return true;
            }
            return null;
        }

        #endregion

        #region API

        object OnPopulateBetterLoot(LootContainer container)
        {
            if (IsRunning && Vector3.Distance(container.transform.position, start_button.transform.position) < config.max_distance) return true;
            return null;
        }
        object CanPopulateLoot(LootContainer container)
        {
            if (IsRunning && Vector3.Distance(container.transform.position, start_button.transform.position) < config.max_distance) return false;
            return null;
        }

        void EMEndGame(string eventName)
        {
            if (eventName == this.Name)
            {
                EndHungerGames();
            }
        }

        void EMStartNextEvent(string eventName)
        {
            if (eventName == this.Name)
            {
                InitiateGame();
            }
        }

        [HookMethod("AllowLootPlayer")]
        object AllowLootPlayer(BasePlayer target, BasePlayer player)
        {
            if (Participants.Contains(player) && Participants.Contains(target)) return true;
            if (pcdData.Top_Centre_Point != null && Vector3.Distance(player.transform.position, pcdData.Top_Centre_Point) < config.max_distance) return true;
            return null;
        }

        [HookMethod("AllowLootBag")]
        object AllowLootBag(BasePlayer player, DroppedItemContainer container)
        {
            if (Participants.Contains(player) && pcdData.Top_Centre_Point != null && Vector3.Distance(player.transform.position, pcdData.Top_Centre_Point) < config.max_distance) return true;
            return null;
        }

        [HookMethod("AllowLootCorpse")]
        object AllowLootCorpse(BasePlayer player, LootableCorpse corpse)
        {
            if (Participants.Contains(player) && pcdData.Top_Centre_Point != null && Vector3.Distance(player.transform.position, pcdData.Top_Centre_Point) < config.max_distance) return true;
            return null;
        }

        [HookMethod("isGameRunning")]
        public bool isGameRunning()
        {
            if (IsRunning) return true;
            return false;
        }

        [HookMethod("GetLeaveCommand")]
        public string GetLeaveCommand()
        {
            return config.leave_command;
        }

        [HookMethod("PlayerRosteredOn")]
        public bool PlayerRosteredOn(BasePlayer player)
        {
            return Participants.Contains(player);
        }

        object CanRedeemKit(BasePlayer player)
        {
            if (Participants.Contains(player)) return false;
            return null;
        }

        void PlayerLeftEvent(BasePlayer player)
        {
            if (Participants.Contains(player))
            {
                Participants.Remove(player);
                CuiHelper.DestroyUi(player, "BleedingPanel");
                if (NightVision != null)
                {
                    NightVision.Call("UnlockPlayerTime", player);
                }
                CheckWin();
                PrintToChat(player, string.Format(lang.GetMessage("hg33", this, player.UserIDString), Prefix));
            }
        }

        object OnLifeSupportSavingLife(BasePlayer player)
        {
            if (Participants.Contains(player))
            {
                return false;
            }
            return null;
        }

        #endregion

        #region Loot handling

        List<Vector3> ConvertLocalsToWorld(List<Vector3> locs)
        {
            var locations = new List<Vector3>();
            foreach (var loc in locs)
            {
                locations.Add(start_button.transform.localToWorldMatrix.MultiplyPoint3x4(loc));
                Puts($"Added {locations.Last()}[Local: {loc}]. Dist from centre: {Vector3.Distance(locations.Last(), pcdData.Top_Centre_Point)}");
            }
            return locations;
        }

        Vector3 ConvertLocalsToWorld(Vector3 loc)
        {
            var pos = start_button.transform.localToWorldMatrix.MultiplyPoint3x4(loc);
            return pos;
        }

        List<Vector3> default_loot_spawns
        {
            get
            {
                return new List<Vector3>()
                {
                    new Vector3(176.321f, 17.329f, -4.215f),
                    new Vector3(173.067f, 17.329f, -4.215f),
                    new Vector3(93.91f, 15.84f, 51.374f),
                    new Vector3(93.577f, 18.83f, 51.711f),
                    new Vector3(-118.541f, 15.384f, -17.733f),
                    new Vector3(-121.226f, 14.772f, -18.577f),
                    new Vector3(-95.128f, 19.28f, -98.125f),
                    new Vector3(-95.79f, 19.28f, -95.775f),
                    new Vector3(-61.994f, 36.243f, 84.573f),
                    new Vector3(-58.994f, 39.393f, 78.788f),
                    new Vector3(-63.299f, 39.393f, 80.492f),
                    new Vector3(64.405f, 30.637f, -56.252f),
                    new Vector3(27.113f, 27.039f, -89.999f),
                    new Vector3(59.493f, 22.903f, -151.156f),
                    new Vector3(67.86f, 13.71f, -124.461f),
                    new Vector3(198.127f, 14.198f, 54.266f),
                    new Vector3(49.509f, 13.228f, 198.994f),
                    new Vector3(81.52f, 27.07f, 109.722f),
                    new Vector3(86.958f, 17.301f, 158.791f),
                    new Vector3(163.477f, 13.665f, 54.945f),
                    new Vector3(129.988f, 13.737f, -93.77f),
                    new Vector3(97.594f, 18.961f, 16.525f),
                    new Vector3(-119.988f, 14.486f, 165.214f),
                    new Vector3(-85.732f, 14.863f, 139.08f),
                    new Vector3(-57.532f, 26.131f, 113.947f),
                    new Vector3(-26.207f, 16.137f, 133.352f),
                    new Vector3(-42.175f, 14.392f, 165.379f),
                    new Vector3(-27.147f, 14.796f, 198.795f),
                    new Vector3(19.623f, 13.999f, 190.549f),
                    new Vector3(47.368f, 17.071f, 57.322f),
                    new Vector3(-23.136f, 30.15f, 114.95f),
                    new Vector3(3.01f, 14.692f, 119.689f),
                    new Vector3(43.844f, 14.953f, 145.943f),
                    new Vector3(96.688f, 18.495f, 165.287f),
                    new Vector3(131.076f, 16.076f, 145.69f),
                    new Vector3(101.108f, 20.199f, 94.576f),
                    new Vector3(155.921f, 20.7f, 89.393f),
                    new Vector3(188.599f, 15.422f, 9.629f),
                    new Vector3(133.686f, 16.118f, 9.919f),
                    new Vector3(181.615f, 22.678f, -40.361f),
                    new Vector3(157.63f, 14.04f, -33.058f),
                    new Vector3(169.093f, 12.878f, -87.981f),
                    new Vector3(105.79f, 15.937f, -72.671f),
                    new Vector3(115.782f, 15.535f, -133.955f),
                    new Vector3(83.749f, 14.252f, -160.763f),
                    new Vector3(62.05f, 29.012f, -99.035f),
                    new Vector3(49.563f, 32.414f, -88.962f),
                    new Vector3(-12.291f, 27.392f, -74.769f),
                    new Vector3(-5.897f, 17.523f, -124.691f),
                    new Vector3(-28.209f, 13.612f, -170.835f),
                    new Vector3(-61.328f, 21.393f, -75.259f),
                    new Vector3(-94.839f, 15.927f, -134.167f),
                    new Vector3(-121.417f, 15.916f, -119.419f),
                    new Vector3(-91.281f, 18.792f, -40.067f),
                    new Vector3(-155.026f, 14.341f, -77.2f),
                    new Vector3(-174.586f, 14.714f, -34.213f),
                    new Vector3(-145.34f, 13.278f, -16.81f),
                    new Vector3(-94.554f, 15f, 4.502f),
                    new Vector3(-131.765f, 14.631f, 23.371f),
                    new Vector3(-179.229f, 16.65f, 20.204f),
                    new Vector3(-163.217f, 17.051f, 56.512f),
                    new Vector3(-94.919f, 14.29f, 79.576f),
                    new Vector3(-153.419f, 14.615f, 97.379f),
                    new Vector3(-121.003f, 14.419f, 146.684f),
                    new Vector3(-74.679f, 20.86f, 55.022f),
                    new Vector3(73.469f, 20.966f, 84.33f),
                    new Vector3(-24.491f, 21.158f, 86.068f),
                    new Vector3(5.705f, 22.454f, -68.509f),
                    new Vector3(58.408f, 28.406f, -23.251f),
                    new Vector3(-68.787f, 14.186f, -26.727f),
                    new Vector3(-1.078f, 19.199f, 3.285f),
                    new Vector3(0.698f, 19.227f, 1.829f),
                    new Vector3(-2.023f, 19.187f, 0.964f),
                    new Vector3(-0.604f, 20.732f, 1.769f)
                };
            }
        }

        Dictionary<string, Dictionary<string, int>> default_loot_profiles
        {
            get
            {
                return new Dictionary<string, Dictionary<string, int>>()
                {
                    ["PrimitiveLoot"] = new Dictionary<string, int>()
                    {
                        ["attire.hide.pants"] = 1,
                        ["attire.hide.poncho"] = 1,
                        ["attire.hide.skirt"] = 1,
                        ["attire.hide.vest"] = 1,
                        ["attire.hide.helterneck"] = 1,
                        ["attire.hide.boots"] = 1,
                        ["grenade.beancan"] = 1,
                        ["hat.wolf"] = 1,
                        ["spear.stone"] = 1,
                        ["spear.wooden"] = 1,
                        ["arrow.bone"] = 20,
                        ["arrow.fire"] = 5,
                        ["arrow.hv"] = 10,
                        ["arrow.wooden"] = 20,
                        ["longsword"] = 1,
                        ["salvaged.sword"] = 1,
                        ["machete"] = 1,
                        ["bow.compound"] = 1,
                        ["crossbow"] = 1,
                        ["bow.hunting"] = 1,
                        ["grenade.f1"] = 3,
                        ["pistol.revolver"] = 1,
                        ["ammo.pistol"] = 10,
                        ["pistol.nailgun"] = 1,
                        ["bandage"] = 3,
                        ["syringe.medical"] = 2,
                        ["bone.armor.suit"] = 1,
                        ["bone.club"] = 1,
                        ["deer.skull.mask"] = 1,
                        ["knife.bone"] = 1,
                        ["wood.armor.helmet"] = 1,
                        ["wood.armor.pants"] = 1,
                        ["wood.armor.jacket"] = 1,
                        ["hat.boonie"] = 1,
                        ["bucket.helmet"] = 1,
                        ["riot.helmet"] = 1,
                        ["burlap.gloves.new"] = 1,
                        ["burlap.headwrap"] = 1,
                        ["burlap.shirt"] = 1,
                        ["burlap.shoes"] = 1,
                        ["burlap.trousers"] = 1,
                        ["knife.butcher"] = 1,
                        ["knife.combat"] = 1,
                        ["shotgun.waterpipe"] = 1,
                        ["ammo.handmade.shell"] = 5,
                        ["stonehatchet"] = 1,
                        ["mace"] = 1,
                        ["salvaged.cleaver"] = 1,
                        ["rock"] = 1,
                        ["sickle"] = 1
                    },
                    ["GunLoot"] = new Dictionary<string, int>()
                    {
                        ["weapon.mod.8x.scope"] = 1,
                        ["weapon.mod.small.scope"] = 1,
                        ["crossbow"] = 1,
                        ["weapon.mod.holosight"] = 1,
                        ["longsword"] = 1,
                        ["machete"] = 1,
                        ["weapon.mod.muzzleboost"] = 1,
                        ["weapon.mod.muzzlebrake"] = 1,
                        ["salvaged.cleaver"] = 1,
                        ["weapon.mod.silencer"] = 1,
                        ["weapon.mod.simplesight"] = 1,
                        ["tactical.gloves"] = 1,
                        ["weapon.mod.lasersight"] = 1,
                        ["ammo.shotgun"] = 10,
                        ["ammo.shotgun.fire"] = 10,
                        ["ammo.shotgun.slug"] = 5,
                        ["ammo.grenadelauncher.he"] = 3,
                        ["ammo.rifle"] = 30,
                        ["ammo.rifle.explosive"] = 10,
                        ["ammo.handmade.shell"] = 20,
                        ["ammo.rocket.hv"] = 2,
                        ["ammo.rifle.hv"] = 10,
                        ["ammo.pistol.fire"] = 10,
                        ["ammo.nailgun.nails"] = 50,
                        ["ammo.pistol"] = 30,
                        ["ammo.pistol.hv"] = 20,
                        ["ammo.rifle.incendiary"] = 10,
                        ["rifle.ak"] = 1,
                        ["rifle.bolt"] = 1,
                        ["rifle.l96"] = 1,
                        ["rifle.lr300"] = 1,
                        ["rifle.m39"] = 1,
                        ["rifle.semiauto"] = 1,
                        ["pistol.eoka"] = 1,
                        ["pistol.m92"] = 1,
                        ["pistol.nailgun"] = 1,
                        ["pistol.python"] = 1,
                        ["pistol.revolver"] = 1,
                        ["pistol.semiauto"] = 1,
                        ["arrow.bone"] = 40,
                        ["arrow.fire"] = 10,
                        ["arrow.hv"] = 40,
                        ["arrow.wooden"] = 60,
                        ["jumpsuit.suit.blue"] = 1,
                        ["bone.armor.suit"] = 1,
                        ["hazmatsuit"] = 1,
                        ["hazmatsuit.nomadsuit"] = 1,
                        ["hazmatsuit.spacesuit"] = 1,
                        ["roadsign.jacket"] = 1,
                        ["roadsign.kilt"] = 1,
                        ["wood.armor.helmet"] = 1,
                        ["wood.armor.pants"] = 1,
                        ["wood.armor.jacket"] = 1,
                        ["deer.skull.mask"] = 1,
                        ["bucket.helmet"] = 1,
                        ["coffeecan.helmet"] = 1,
                        ["heavy.plate.helmet"] = 1,
                        ["riot.helmet"] = 1,
                        ["shotgun.double"] = 1,
                        ["shotgun.pump"] = 1,
                        ["shotgun.spas12"] = 1,
                        ["shotgun.waterpipe"] = 1,
                        ["smg.2"] = 1,
                        ["smg.mp5"] = 1,
                        ["smg.thompson"] = 1,
                        ["grenade.f1"] = 5,
                        ["multiplegrenadelauncher"] = 1,
                        ["grenade.smoke"] = 2,
                        ["grenade.beancan"] = 1,
                        ["rocket.launcher"] = 1,
                        ["jacket"] = 1,
                        ["burlap.gloves.new"] = 1,
                        ["burlap.gloves"] = 1,
                        ["roadsign.gloves"] = 1,
                        ["shoes.boots"] = 1,
                        ["boots.frog"] = 1,
                        ["attire.hide.boots"] = 1,
                        ["attire.hide.pants"] = 1,
                        ["attire.hide.poncho"] = 1,
                        ["attire.hide.skirt"] = 1,
                        ["attire.hide.vest"] = 1,
                        ["pants"] = 1,
                        ["pants.shorts"] = 1,
                        ["hoodie"] = 1,
                        ["syringe.medical"] = 4,
                        ["bandage"] = 6,
                        ["largemedkit"] = 2,
                        ["metal.plate.torso"] = 1,
                        ["metal.facemask"] = 1
                    }
                };
            }
        }

        List<LootContainer> spawned_Crates = new List<LootContainer>();

        void ClearCrates()
        {
            Unsubscribe("OnEntityKill");            
            if (spawned_Crates.Count > 0)
            {
                foreach (var box in spawned_Crates.ToList())
                {
                    box.KillMessage();
                }
                spawned_Crates.Clear();
            }           
            crate_respawn_timer.Clear();
        }

        Dictionary<Vector3, float> crate_respawn_timer = new Dictionary<Vector3, float>();

        void CheckCrateTimers()
        {
            if (crate_respawn_timer.Count == 0) return;
            foreach (var crate in crate_respawn_timer.ToList())
            {
                if (Time.time > crate.Value)
                {
                    SpawnCrate(crate.Key);
                    crate_respawn_timer.Remove(crate.Key);
                }
            }
        }

        Timer loot_refill_timer;

        void AddCrates()
        {
            if (loot_refill_timer != null && !loot_refill_timer.Destroyed) loot_refill_timer.Destroy();
            loot_refill_timer = timer.Every(10f, () =>
            {
                CheckCrateTimers();
            });
            foreach (var crate in loot_positions)
            {
                SpawnCrate(crate);
            }
        }

        void SpawnCrate(Vector3 pos)
        {
            if (Vector3.Distance(pos, pcdData.Top_Centre_Point) > config.max_distance) return;
            var entity = GameManager.server.CreateEntity("assets/bundled/prefabs/radtown/crate_normal_2.prefab", pos);
            entity.Spawn();
        }

        void OnEntityKill(LootContainer entity)
        {
            if (entity != null && entity.net != null)
            {
                spawned_Crates.Remove(entity);
                var random_time = Time.time + UnityEngine.Random.Range(config.respawn_delay_min, config.respawn_delay_max);
                if (!crate_respawn_timer.ContainsKey(entity.transform.position)) crate_respawn_timer.Add(entity.transform.position, random_time);
                else crate_respawn_timer[entity.transform.position] = random_time;
            }
        }

        private object OnContainerPopulate(LootContainer container)
        {
            if (Vector3.Distance(container.transform.position, start_button.transform.position) < config.max_distance) return true;
            return null;
        }

        private void OnLootSpawn(LootContainer container)
        {
            if (!config.use_config_loot) return;
            // return true when populating our own loot.
            if (!IsRunning) return;
            if (container == null) return;
            if (Vector3.Distance(container.transform.position, start_button.transform.position) < config.max_distance)
            {
                timer.Once(0.1f, () =>
                {
                    if (container == null) return;
                    container.inventory.Clear();
                    if (loot_profile == null) loot_profile = config.loot_profiles.First().Key;
                    var slots = UnityEngine.Random.Range(config.min_loot_amount, config.max_loot_amount + 1);
                    var profile = config.loot_profiles[loot_profile];
                    for (int i = 0; i < slots; i++)
                    {
                        var random_item = profile.ToList().GetRandom();
                        var loot = ItemManager.CreateByName(random_item.Key, random_item.Value);
                        if (!loot.MoveToContainer(container.inventory)) loot.Remove();
                    }
                    spawned_Crates.Add(container);
                });                
            }
        }

        #endregion
    }
}
