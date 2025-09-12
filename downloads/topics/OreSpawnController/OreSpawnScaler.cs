using System;
using Oxide.Core.Plugins;
using Oxide.Core;
using UnityEngine;
using System.Collections.Generic;
using ConVar;
using System.Text;
using System.IO;

namespace Oxide.Plugins
{
    [Info("OreSpawnScaler", "Richard Hertz", "1.0.2")]
    [Description("Increases the amount of nodes that spawn")]

    public class OreSpawnScaler : RustPlugin
    {
        private ConfigData configData;
        private HashSet<BaseEntity> spawnedNodes = new HashSet<BaseEntity>();
        private int deployedMask;


        private const string ConstructionLayer = "Construction";
        private const string DeployedLayer = "Deployed";
        private const string RoadLayer = "Road";

        private int constructionLayerMask;
        private int deployedLayerMask;
        private int roadLayerMask;

        public class ConfigData
        {
            public OreConfig Stone { get; set; }
            public OreConfig Metal { get; set; }
            public OreConfig Sulfur { get; set; }
            public bool Debug { get; set; }

            public class OreConfig
            {
                public float GridSize { get; set; }
                public string Prefab { get; set; }
                public string SnowPrefab { get; set; }
                public BiomeProbability BiomeProbabilities { get; set; }

                public class BiomeProbability
                {
                    public float AridBiome { get; set; }
                    public float TemperateBiome { get; set; }
                    public float ArcticBiome { get; set; }
                    public float TundraBiome { get; set; }
                }
            }
        }

        public enum BiomeType
        {
            None,
            Arid,
            Temperate,
            Arctic,
            Tundra
        }

        private class PositionInfo
        {
            public Vector3 Position { get; set; }
            public ConfigData.OreConfig Config { get; set; }
            public BiomeType BiomeType { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new ConfigData
            {
                Stone = new ConfigData.OreConfig
                {
                    GridSize = 100.0f,
                    Prefab = "assets/bundled/prefabs/autospawn/resource/ores/stone-ore.prefab",
                    SnowPrefab = "assets/bundled/prefabs/autospawn/resource/ores_snow/stone-ore.prefab",
                    BiomeProbabilities = new ConfigData.OreConfig.BiomeProbability
                    {
                        AridBiome = 0.25f,
                        TemperateBiome = 0.25f,
                        ArcticBiome = 0.25f,
                        TundraBiome = 0.25f
                    }
                },
                Metal = new ConfigData.OreConfig
                {
                    GridSize = 100.0f,
                    Prefab = "assets/bundled/prefabs/autospawn/resource/ores/metal-ore.prefab",
                    SnowPrefab = "assets/bundled/prefabs/autospawn/resource/ores_snow/metal-ore.prefab",
                    BiomeProbabilities = new ConfigData.OreConfig.BiomeProbability
                    {
                        AridBiome = 0.25f,
                        TemperateBiome = 0.25f,
                        ArcticBiome = 0.25f,
                        TundraBiome = 0.25f
                    }
                },
                Sulfur = new ConfigData.OreConfig
                {
                    GridSize = 100.0f,
                    Prefab = "assets/bundled/prefabs/autospawn/resource/ores/sulfur-ore.prefab",
                    SnowPrefab = "assets/bundled/prefabs/autospawn/resource/ores_snow/sulfur-ore.prefab",
                    BiomeProbabilities = new ConfigData.OreConfig.BiomeProbability
                    {
                        AridBiome = 0.25f,
                        TemperateBiome = 0.25f,
                        ArcticBiome = 0.25f,
                        TundraBiome = 0.25f
                    }
                },
                Debug = false
            }, true);
        }

        private Queue<PositionInfo> positions;


        private void Init()
        {
            configData = Config.ReadObject<ConfigData>();
            positions = new Queue<PositionInfo>();
            constructionLayerMask = LayerMask.GetMask(ConstructionLayer);
            deployedLayerMask = LayerMask.GetMask(DeployedLayer);
            roadLayerMask = LayerMask.GetMask(RoadLayer);
            permission.RegisterPermission("orespawnscaler.show", this);
        }

        private void OnServerInitialized()
        {
            Puts("Starting OreSpawnScaler...");

            GeneratePositions(configData.Stone);
            GeneratePositions(configData.Metal);
            GeneratePositions(configData.Sulfur);

            timer.Every(0.1f, ProcessQueue);
        }

        [ChatCommand("nodes_show")]
        private void NodesShowCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "orespawnscaler.show"))
            {
                SendReply(player, "You don't have permission to use this command.");
                return;
            }

            foreach (BaseEntity node in spawnedNodes)
            {
                if (node != null)
                {
                    Sphere(player, node.transform.position, 3f, UnityEngine.Color.green, 30f);
                }
            }

            SendReply(player, "Nodes have been highlighted for 30 seconds.");
        }

        public void Sphere(BasePlayer player, Vector3 pos, float radius, UnityEngine.Color color, float duration)
        {
            player.SendConsoleCommand("ddraw.sphere", duration, color, pos, radius);
        }

        private void GeneratePositions(ConfigData.OreConfig config)
        {
            float halfMapSizeX = TerrainMeta.Size.x / 2;
            float halfMapSizeZ = TerrainMeta.Size.z / 2;

            for (float x = -halfMapSizeX; x < halfMapSizeX; x += config.GridSize)
            {
                for (float z = -halfMapSizeZ; z < halfMapSizeZ; z += config.GridSize)
                {
                    Vector3 position = new Vector3(x, 0, z);
                    position.y = TerrainMeta.HeightMap.GetHeight(position);

                    if (IsPositionSuitable(position, config.Prefab))
                    {
                        positions.Enqueue(new PositionInfo
                        {
                            Position = position,
                            Config = config,
                            BiomeType = GetBiomeType(position)
                        });
                    }
                }
            }
        }

        private void ProcessQueue()
        {
            if (positions.Count > 0)
            {
                PositionInfo posInfo = positions.Dequeue();
                ConfigData.OreConfig config = posInfo.Config;
                Vector3 basePosition = posInfo.Position;

                float xOffset = UnityEngine.Random.Range(0f, config.GridSize);
                float zOffset = UnityEngine.Random.Range(0f, config.GridSize);
                Vector3 position = new Vector3(basePosition.x + xOffset, basePosition.y, basePosition.z + zOffset);
                position.y = TerrainMeta.HeightMap.GetHeight(position);

                if (IsPositionSuitable(position, config.Prefab))
                {
                    float biomeSpawnProbability = GetBiomeSpawnProbability(config, posInfo.BiomeType);
                    if (UnityEngine.Random.value <= biomeSpawnProbability)
                    {
                        SpawnOreNode(position, config, posInfo.BiomeType);
                    }
                }
            }
        }

        public BiomeType GetBiomeType(Vector3 position)
        {
            if (TerrainMeta.BiomeMap.GetBiome(position, 1) > 0.5f)
            {
                return BiomeType.Arid;
            }
            else if (TerrainMeta.BiomeMap.GetBiome(position, 2) > 0.5f)
            {
                return BiomeType.Temperate;
            }
            else if (TerrainMeta.BiomeMap.GetBiome(position, 4) > 0.5f)
            {
                return BiomeType.Tundra;
            }
            else if (TerrainMeta.BiomeMap.GetBiome(position, 8) > 0.5f)
            {
                return BiomeType.Arctic;
            }

            return BiomeType.None;
        }

        private float GetBiomeSpawnProbability(ConfigData.OreConfig config, BiomeType biomeType)
        {
            switch (biomeType)
            {
                case BiomeType.Arid: return config.BiomeProbabilities.AridBiome;
                case BiomeType.Temperate: return config.BiomeProbabilities.TemperateBiome;
                case BiomeType.Arctic: return config.BiomeProbabilities.ArcticBiome;
                case BiomeType.Tundra: return config.BiomeProbabilities.TundraBiome;
                default: return 0f;
            }
        }

        private bool IsPositionSuitable(Vector3 position, string prefab)
        {
            UnityEngine.RaycastHit hit;
            if (!UnityEngine.Physics.Raycast(new Vector3(position.x, 1000, position.z), Vector3.down, out hit, Mathf.Infinity, LayerMask.GetMask("Terrain", "World")))
            {
                return false;
            }

            position.y = hit.point.y;

            if (position.y <= 1)
                return false;

            //if (WaterLevel.Test(position))
            // return false;

            int topology = TerrainMeta.TopologyMap.GetTopology(position);

            if ((topology & (int)TerrainTopology.Enum.Building) != 0)
                return false;

            if ((topology & (int)TerrainTopology.Enum.Beachside) != 0)
                return false;

            if ((topology & (int)TerrainTopology.Enum.Monument) != 0)
                return false;

            if ((topology & (int)TerrainTopology.Enum.Road) != 0)
                return false;

            if ((topology & (int)TerrainTopology.Enum.Roadside) != 0)
                return false;

            if ((topology & (int)TerrainTopology.Enum.Rail) != 0)
                return false;

            if ((topology & (int)TerrainTopology.Enum.Railside) != 0)
                return false;

            if ((topology & (int)TerrainTopology.Enum.River) != 0)
                return false;

            if ((topology & (int)TerrainTopology.Enum.Decor) != 0)
                return false;

            Collider[] playerColliders = UnityEngine.Physics.OverlapSphere(position, 75f, LayerMask.GetMask("Player (Server)"));
            if (playerColliders.Length > 0)
                return false;

            Collider[] buildingBlockColliders = UnityEngine.Physics.OverlapSphere(position, 75f, LayerMask.GetMask("Construction"));
            if (buildingBlockColliders.Length > 0)
                return false;

            return true;
        }

        private void SpawnOreNode(Vector3 position, ConfigData.OreConfig config, BiomeType biome)
        {
            string prefab;

            if (biome == BiomeType.Arctic)
            {
                prefab = config.SnowPrefab;
            }
            else
            {
                prefab = config.Prefab;
            }

            BaseEntity entity = GameManager.server.CreateEntity(prefab, position);
            if (entity != null)
            {
                entity.Spawn();
                spawnedNodes.Add(entity);
                if (configData.Debug)
                {
                    StringBuilder logMessage = new StringBuilder();
                    logMessage.Append("Spawned ");
                    logMessage.Append(prefab);
                    logMessage.Append(" at ");
                    logMessage.Append(position.ToString());
                    Puts(logMessage.ToString());
                }
            }
        }

        private void Unload()
        {
            foreach (BaseEntity entity in spawnedNodes)
            {
                entity.Kill();
            }
        }
    }
}
