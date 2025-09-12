using System;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using Newtonsoft.Json.Converters;
using Facepunch;
using VLB;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rust;

namespace Oxide.Plugins
{
    [Info("Barrel Spawn", "xoboran", "1.0.2")]
    [Description("Spawns loot barrels in forest areas of the map so you can loot in the forest without roads.")]
    class BarrelSpawn : RustPlugin
    {
        static BarrelSpawn ins;
        PluginConfig config;

        public class PluginConfig
        {
            [JsonProperty("Barrels respawn time (If the number of active players on your server is high, keep the spawn time short, if the number of players is low, increase the spawn time)")]
            public float RespawnTime;
            [JsonProperty("Minimum distance between barrels (Minimum 100 is recommended)")]
            public float Distance;
        }

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig
            {
                RespawnTime = 600,
                Distance = 100
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        Coroutine coroutine;
        List<Vector3> Spawnpoint = new List<Vector3>();

        void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("BarrelSpawnPoint"))
            {
                Spawnpoint = Interface.Oxide.DataFileSystem.ReadObject<List<Vector3>>("BarrelSpawnPoint");
            }
            else
            {
                Interface.Oxide.DataFileSystem.WriteObject("BarrelSpawnPoint", Spawnpoint = new List<Vector3>());
            }
        }

        private void OnServerInitialized()
        {
            ins = this;
            LoadConfig();
            LoadData();
            if (ConVar.Server.level == "Procedural Map") return;
            coroutine = ServerMgr.Instance.StartCoroutine(spawnBarrel());
        }

        void Unload() 
        {
            if (coroutine != null)
                ServerMgr.Instance.StopCoroutine(coroutine);

            Interface.Oxide.DataFileSystem.WriteObject("BarrelSpawnPoint", Spawnpoint);
            
              ins = null;
        }

        void OnNewSave()
        {
            Interface.Oxide.DataFileSystem.WriteObject("BarrelSpawnPoint", Spawnpoint = new List<Vector3>());
        }

        Vector3 RandomCircle(Vector3 center, float radius)
        {
            float ang = UnityEngine.Random.value * 360;
            float distance = UnityEngine.Random.Range(5, radius);
            Vector3 pos;
            pos.x = center.x + distance * Mathf.Sin(ang * Mathf.Deg2Rad);
            pos.z = center.z + distance * Mathf.Cos(ang * Mathf.Deg2Rad);
            pos.y = center.y;
            pos.y = GetGroundPosition(pos);
            return pos;
        }

        float GetGroundPosition(Vector3 pos)
        {
            float y = TerrainMeta.HeightMap.GetHeight(pos);
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 200f, pos.z), Vector3.down, out hit, Mathf.Infinity,
                LayerMask.GetMask(new[] { "Terrain" })) && !hit.collider.name.Contains("rock_cliff")) return Mathf.Max(hit.point.y, y);
            return y;
        }
        

        private bool TestPosIsValid(Vector3 position)
        {
            var resources = new List<CollectibleEntity>();
            Vis.Entities(position, config.Distance, resources);
            if (resources.Where(x => x.ShortPrefabName.Contains("barrel")).Count() > 0)
                return false;

            return true;
        }
           

        List<string> barrelPrefab = new List<string>()
        {
            "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-1.prefab",
            "assets/bundled/prefabs/autospawn/resource/loot/loot-barrel-2.prefab",

        };


        IEnumerator spawnBarrel()
        {
            var biomMap = TerrainMeta.Terrain.GetComponent<TerrainBiomeMap>();
            var tundraEntity = BaseCombatEntity.serverEntities.entityList.Where(x => x.Value.PrefabName.Contains("v3_tundra_forestside"));
            if (tundraEntity.Count() > Spawnpoint.Count)
            {
                Spawnpoint.Clear();
                foreach (var mm in tundraEntity)
                {
                    if (biomMap.GetBiome(mm.Value.transform.position, 4) >= 0.98f)
                    {
                        var pos = RandomCircle(mm.Value.transform.position, 10);
                        Spawnpoint.Add(pos);
                    }
                    yield return null;
                }
            }

            foreach (var barrel in Spawnpoint)
            {
                if (TestPosIsValid(barrel))
                {
                    var ent = GameManager.server.CreateEntity(barrelPrefab.GetRandom(), barrel);
                    ent.Spawn();
                }
                yield return null;
            }

            yield return CoroutineEx.waitForSecondsRealtime(config.RespawnTime);

            coroutine = ServerMgr.Instance.StartCoroutine(spawnBarrel());
        }
    }
}