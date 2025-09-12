/*▄▄▄    ███▄ ▄███▓  ▄████  ▄▄▄██▀▀▀▓█████▄▄▄█████▓
▓█████▄ ▓██▒▀█▀ ██▒ ██▒ ▀█▒   ▒██   ▓█   ▀▓  ██▒ ▓▒
▒██▒ ▄██▓██    ▓██░▒██░▄▄▄░   ░██   ▒███  ▒ ▓██░ ▒░
▒██░█▀  ▒██    ▒██ ░▓█  ██▓▓██▄██▓  ▒▓█  ▄░ ▓██▓ ░ 
░▓█  ▀█▓▒██▒   ░██▒░▒▓███▀▒ ▓███▒   ░▒████▒ ▒██▒ ░ 
░▒▓███▀▒░ ▒░   ░  ░ ░▒   ▒  ▒▓▒▒░   ░░ ▒░ ░ ▒ ░░   
▒░▒   ░ ░  ░      ░  ░   ░  ▒ ░▒░    ░ ░  ░   ░    
 ░    ░ ░      ░   ░ ░   ░  ░ ░ ░      ░    ░      
 ░             ░         ░  ░   ░      ░  ░
Chat Command:
 /showspawns (view distance)  -  Require Admin, If no view distance passed will use 500.

Perms:
 BeachSpawns.Use  -  Required to use generated spawn points.
 BeachSpawns.Nolimit -  Removes spawn check other then not inside building blocks.
*/
using Newtonsoft.Json;
using Rust;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BeachSpawns", "bmgjet", "1.1.1")]
    [Description("Improved Spawn Points")]
    class BeachSpawns : RustPlugin
    {
        public List<Vector3> SpawnPoints = new List<Vector3>();
        private Coroutine Coroutine;
        private Timer ddraw;

        #region Permissions
        private const string PermUse = "BeachSpawns.Use";
        private const string PermNolimit = "BeachSpawns.Nolimit";
        #endregion

        #region Configuration
        private Configuration config;
        private class Configuration
        {
            [JsonProperty("Scan Map Step Size (3f = foundation sized steps)")]
            public int ScanSpread = 5;

            [JsonProperty("Clear Space Distance Check")]
            public int ClearspaceDistance = 50;

            [JsonProperty("Min Distance Allowed Between Last Corpse And New Spawn")]
            public int MinSpawnDistance = 100;

            [JsonProperty("Block Arctic Biome")]
            public bool BlockArctic = true;

            [JsonProperty("Arctic Biome Switch Point")]
            public float ArcticTrigger = 0.5f;

            [JsonProperty("Block Arid biome")]
            public bool BlockArid = false;

            [JsonProperty("Arid Biome Switch Point")]
            public float AridTrigger = 0.7f;

            [JsonProperty("Block Tundra biome")]
            public bool BlockTundra = false;

            [JsonProperty("Tundra Biome Switch Point")]
            public float TundraTrigger = 0.5f;

            [JsonProperty("Block Monument Topology")]
            public bool BlockMonument = true;

            [JsonProperty("Block Safe Zones")]
            public bool BlockSafeZone = true;

            [JsonProperty("Allow On River Side Topology")]
            public bool AllowRiver = false;

            [JsonProperty("Allow On Lake Side Topology")]
            public bool AllowLake = false;

            [JsonProperty("Max Spawn Attemps")]
            public int CheckAttemps = 128;

            [JsonProperty("Topology Scan Checker Loops (Lower value if server lags)")]
            public int CoroutineCheck = 4096;

            [JsonProperty("Spawn Based On Height Instead Ff Topology (Only use on really bad maps)")]
            public bool SpawnOnHeight = false;

            [JsonProperty("Spawn On Height Max Height Allowed")]
            public int SpawnOnHeightMax = 3;

            [JsonProperty("Min FPS To Generate Spawn Points At Full Speed")]
            public int MinFPS = 20;

            public string ToJson() => JsonConvert.SerializeObject(this);
            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() { config = new Configuration(); }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) { throw new JsonException(); }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                PrintWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }
        protected override void SaveConfig()
        {
            PrintWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }
        #endregion Configuration


        #region Oxide Hooks
        private void OnServerInitialized(bool initial)
        {
            permission.RegisterPermission(PermUse, this);
            permission.RegisterPermission(PermNolimit, this);
            Coroutine = ServerMgr.Instance.StartCoroutine(FindPosition(initial)); //Start Up Spawn Point Generator
        }

        private object OnPlayerRespawn(BasePlayer player, BasePlayer.SpawnPoint spawn) //Player Pressed Respawn
        {
            if (SpawnPoints.Count < 2 || !HasPerm(PermUse, player.UserIDString)) { return null; } //Not enough spawn points or no perm
            for (int i = 0; i < config.CheckAttemps; i++) //Try find valid spawn point within allowed amount of tryss
            {
                Vector3 pos = SpawnPoints.GetRandom(); //Get random from list.
                if (SafeSpawnZone(pos, player)) //Check if safe to spawn there
                {
                    //Spawn there
                    spawn.pos = pos;
                    return spawn;
                }
            }
            return null; //Native Spawn
        }

        private void Unload()
        {
            if (Coroutine != null) { ServerMgr.Instance.StopCoroutine(Coroutine); } //Stop Spawn Point Generator if running
            if (ddraw != null) //Remove ddraw loop if running
            {
                ddraw.Destroy();
                ddraw = null;
            }
        }

        [ChatCommand("showspawns")] //Show spawn points to admin with ddraw
        private void showspawnpoints(BasePlayer player, string command, string[] Args)
        {
            if (player.IsAdmin)
            {
                if (ddraw != null) //Already running so stop
                {
                    ddraw.Destroy();
                    ddraw = null;
                    player.ChatMessage("Stopped Viewing Spawn Points");
                    return;
                }
                //Get distance from args
                int distance = 500;
                if (Args != null && Args.Length == 1) { distance = int.Parse(Args[0]); }
                //Start drawing points nearby player
                player.ChatMessage("Started Viewing Spawn Points");
                ddraw = timer.Every(3f, () =>
                {
                    if (player == null || !player.IsAlive() && player.IsSleeping()) //Player null, dead or not active
                    {
                        if (ddraw != null)
                        {
                            ddraw.Destroy();
                            ddraw = null;
                            return;
                        }
                    }
                    foreach (Vector3 vector in SpawnPoints)
                    {
                        if (Vector3.Distance(vector, player.transform.position) < distance && SafeSpawnZone(vector))
                        {
                            player.SendConsoleCommand("ddraw.sphere", 3f, Color.blue, vector, config.ScanSpread);
                        }
                    }
                });
            }
        }
        #endregion

        #region Methods
        private bool HasPerm(string Perm, string userid) { return permission.UserHasPermission(userid, Perm) || permission.GroupHasPermission("default", Perm); }

        private bool IsDivisible(int x, int n) { return (x % n) == 0; }

        private bool SafeSpawnZone(Vector3 position, BasePlayer player = null)
        {
            if (AntiHack.TestInsideTerrain(position))
            {
                return false; //Not valid position AntiCheat would kick
            }
            if (player != null && HasPerm(PermNolimit, player.UserIDString))
            {
                //Check valid spot not inside a building block
                List<BuildingBlock> list = Facepunch.Pool.Get<List<BuildingBlock>>();
                Vis.Entities<BuildingBlock>(position, 10, list, -1);
                if (list.Count == 0)
                {
                    Facepunch.Pool.FreeUnmanaged(ref list);
                    return true;
                }
                Facepunch.Pool.FreeUnmanaged(ref list);
                return false;
            }
            if (player != null && player.ServerCurrentDeathNote != null && Vector3.Distance(position, player.ServerCurrentDeathNote.worldPosition) < config.MinSpawnDistance)
            {
                return false; //To close to last corpse
            }
            //Disables respawning when player or threat nearby or a base
            List<BaseEntity> list2 = Facepunch.Pool.Get<List<BaseEntity>>();
            Vis.Entities<BaseEntity>(position, config.ClearspaceDistance, list2, -1);
            foreach (BaseEntity be in list2)
            {
                if (be is BasePlayer && !(be as BasePlayer).IsSleeping() || be is BuildingBlock || be is AutoTurret || be is BaseAnimalNPC || be is ScientistNPC || be is Tugboat)
                {
                    Facepunch.Pool.FreeUnmanaged(ref list2);
                    return false;
                }
            }
            Facepunch.Pool.FreeUnmanaged(ref list2);
            return true;
        }

        private IEnumerator FindPosition(bool initial)
        {
            if (initial)
            {
                yield return CoroutineEx.waitForSeconds(10);  //Wait for server to finish spawning stuff from other plugins
            }
            //Set up vars
            int minPos = (int)(World.Size / -2f);
            int maxPos = (int)(World.Size / 2f);
            int loops = (int)((World.Size / config.ScanSpread) * (World.Size / config.ScanSpread));
            int done = 0;
            int last = 0;
            var checks = 0;
            //Loop whole map in scan steps
            for (float x = minPos; x < maxPos; x += config.ScanSpread) //X grids
            {
                for (float z = minPos; z < maxPos; z += config.ScanSpread) //Z grids
                {
                    done++; //Keep note of position in loop
                    if (++checks >= config.CoroutineCheck) //Check conditions so many loops
                    {
                        //Limit rate based on FPS
                        if (Performance.report.frameRate < config.MinFPS && ConVar.FPS.limit > config.MinFPS) { yield return CoroutineEx.waitForSeconds(0.01f); }
                        else { yield return CoroutineEx.waitForSeconds(0.0035f); }
                        checks = 0;
                        //Output Percentage Debug
                        int percentComplete = (int)Math.Round((double)(100 * done) / loops); //Calculate percentage completed
                        if (last != percentComplete && IsDivisible(percentComplete, 10))
                        {
                            last = percentComplete;
                            Puts("Scanning Spawn Points " + percentComplete + "%");
                        }
                    }
                    Vector3 original = new Vector3(x, 0, z); //Get position
                    original.y = TerrainMeta.HeightMap.GetHeight(original) + 0.05f; //Set ground height slightly raised to prevent clipping
                    //Block below water, biomes, inside prefabs/rocks, monument topo, building topo
                    if (original.y <= 0 || (config.BlockTundra && TerrainMeta.BiomeMap.GetBiome(original, TerrainBiome.TUNDRA) > config.AridTrigger) || (config.BlockArid && TerrainMeta.BiomeMap.GetBiome(original, TerrainBiome.ARID) > config.AridTrigger) || (config.BlockArctic && TerrainMeta.BiomeMap.GetBiome(original, TerrainBiome.ARCTIC) > config.AridTrigger) || GamePhysics.CheckSphere(original, config.ScanSpread, Layers.Mask.Player_Server | Layers.Server.Deployed, QueryTriggerInteraction.Ignore) || AntiHack.IsInsideMesh(original) || (config.BlockMonument && TerrainMeta.TopologyMap.GetTopology(original, TerrainTopology.MONUMENT) || TerrainMeta.TopologyMap.GetTopology(original, TerrainTopology.BUILDING)))
                    {
                        continue;
                    }
                    //Create points based off height, Water level to set max
                    if (config.SpawnOnHeight && original.y <= config.SpawnOnHeightMax)
                    {
                        SpawnPoints.Add(original);
                        continue;
                    }
                    if (!TerrainMeta.TopologyMap.GetTopology(original, TerrainTopology.BEACH) || !TerrainMeta.TopologyMap.GetTopology(original, TerrainTopology.BEACHSIDE))
                    {
                        //Not beach or beachside topo
                        //Check allowed overrides
                        if (config.AllowLake && TerrainMeta.TopologyMap.GetTopology(original, TerrainTopology.LAKESIDE)) { SpawnPoints.Add(original); }
                        else if (config.AllowRiver && TerrainMeta.TopologyMap.GetTopology(original, TerrainTopology.RIVERSIDE)) { SpawnPoints.Add(original); }
                        continue; //Dont add to list
                    }
                    //Block safezone spawn points
                    if (config.BlockSafeZone)
                    {
                        bool SafeZone = false;
                        List<Collider> list = Facepunch.Pool.Get<List<Collider>>();
                        Vis.Colliders<Collider>(original, 3f, list, -1, QueryTriggerInteraction.Collide);
                        using (List<Collider>.Enumerator enumerator = list.GetEnumerator())
                        {
                            while (enumerator.MoveNext())
                            {
                                if (enumerator.Current.GetComponent<TriggerSafeZone>() != null)
                                {
                                    SafeZone = true;
                                    break;
                                }
                            }
                        }
                        Facepunch.Pool.FreeUnmanaged(ref list);
                        if (SafeZone) { continue; }
                    }
                    //Is a valid spawn position
                    SpawnPoints.Add(original);
                }
            }
            Puts("Created " + SpawnPoints.Count.ToString() + " Spawn Points.");
            if (SpawnPoints.Count < 20) { Debug.LogError("[BeachSpawns] Not Many Spawn Points, Consider Adjusting Config File!"); }
            Coroutine = null;
        }
        #endregion
    }
}