using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rust;

namespace Oxide.Plugins
{
    [Info("OreEvent", "REIN", "1.0.15")]
    public class OreEvent : RustPlugin
    {
        private readonly string SulfurOre = "assets/bundled/prefabs/autospawn/resource/ores/sulfur-ore.prefab";
        private readonly string MetalOre = "assets/bundled/prefabs/autospawn/resource/ores/metal-ore.prefab";
        private readonly string StoneOre = "assets/bundled/prefabs/autospawn/resource/ores/stone-ore.prefab";

        private List<BaseEntity> spawnedOres = new List<BaseEntity>();
        private float mapSize;

        private const float MIN_SPAWN_DISTANCE = 15f;
        private const float SPAWN_ATTEMPTS = 100;
        private const float MAX_SLOPE_ANGLE = 25f;
        private const float BUILDING_CHECK_RADIUS = 3f;
        private const float WATER_CHECK_RADIUS = 1f;
        private const float MIN_HEIGHT_ABOVE_WATER = 1f;
        private const float STABILITY_RADIUS = 2f;
        private const float GROUND_OFFSET = 0.25f;

        private readonly int TerrainMask = LayerMask.GetMask("Terrain", "World");
        private readonly int ConstructionMask = LayerMask.GetMask("Construction", "Deployed");

        private int MetalCount => Mathf.RoundToInt(200 * (mapSize / 4000f));
        private int SulfurCount => Mathf.RoundToInt(100 * (mapSize / 4000f));
        private int StoneCount => Mathf.RoundToInt(300 * (mapSize / 4000f));

        void OnServerInitialized(bool initial)
        {
            mapSize = World.Size;
            CleanupAllOres();
            SpawnOresAcrossMap();
            Puts($"Размер карты: {mapSize}");
            Puts($"Количество руд для текущей карты: Металл {MetalCount}, Сера {SulfurCount}, Камень {StoneCount}");
        }

        void Unload()
        {
            CleanupAllOres();
        }

        void CleanupAllOres()
        {
            var ores = UnityEngine.Object.FindObjectsOfType<BaseEntity>();
            int count = 0;

            foreach (var entity in ores)
            {
                if (entity == null || entity.IsDestroyed) continue;
                if (entity.HasFlag(BaseEntity.Flags.Reserved8)) continue;
                if (entity.PrefabName == SulfurOre || entity.PrefabName == MetalOre || entity.PrefabName == StoneOre)
                {
                    entity.Kill();
                    count++;
                }
            }

            spawnedOres.Clear();
            if (count > 0)
                Puts($"Очищено {count} руд при инициализации/выгрузке плагина");
        }

        void SpawnOresAcrossMap()
        {
            Puts("Начинаем спавн руд...");
            int metalSpawned = SpawnSpecificOre(MetalOre, MetalCount);
            int sulfurSpawned = SpawnSpecificOre(SulfurOre, SulfurCount);
            int stoneSpawned = SpawnSpecificOre(StoneOre, StoneCount);
            Puts($"Заспавнено руд: Металл {metalSpawned}/{MetalCount}, Сера {sulfurSpawned}/{SulfurCount}, Камень {stoneSpawned}/{StoneCount}");
        }

        int SpawnSpecificOre(string prefab, int count)
        {
            int spawned = 0;
            for (int i = 0; i < count && i < 1000; i++)
            {
                if (TrySpawnOre(prefab))
                    spawned++;
            }
            return spawned;
        }

        bool TrySpawnOre(string prefab)
        {
            for (int i = 0; i < SPAWN_ATTEMPTS; i++)
            {
                Vector3 randomPos = GetRandomMapPosition();
                Vector3? validPos = ValidateAndAdjustPosition(randomPos);
                
                if (validPos.HasValue && IsValidSpawnPosition(validPos.Value))
                {
                    return SpawnOreAtPosition(prefab, validPos.Value);
                }
            }
            return false;
        }

        Vector3 GetRandomMapPosition()
        {
            float halfSize = mapSize / 2;
            float x = UnityEngine.Random.Range(-halfSize, halfSize);
            float z = UnityEngine.Random.Range(-halfSize, halfSize);
            return new Vector3(x, 0, z);
        }

        Vector3? ValidateAndAdjustPosition(Vector3 position)
        {
            float terrainHeight = TerrainMeta.HeightMap.GetHeight(position);
            position.y = terrainHeight + 50f;

            RaycastHit hit;
            if (!Physics.Raycast(position, Vector3.down, out hit, 100f, TerrainMask))
                return null;

            float slope = Vector3.Angle(hit.normal, Vector3.up);
            if (slope > MAX_SLOPE_ANGLE)
                return null;

            Vector3 adjustedPos = hit.point + (Vector3.up * GROUND_OFFSET);
            
            if (!CheckStability(adjustedPos))
                return null;

            return adjustedPos;
        }

        bool CheckStability(Vector3 position)
        {
            Vector3[] checkPoints = new Vector3[]
            {
                position,
                position + (Vector3.forward * STABILITY_RADIUS),
                position + (Vector3.back * STABILITY_RADIUS),
                position + (Vector3.left * STABILITY_RADIUS),
                position + (Vector3.right * STABILITY_RADIUS)
            };

            foreach (Vector3 point in checkPoints)
            {
                RaycastHit hit;
                if (!Physics.Raycast(point + (Vector3.up * 1f), Vector3.down, out hit, 2f, TerrainMask))
                    return false;

                if (Mathf.Abs(hit.point.y - position.y) > 0.5f)
                    return false;
            }

            return true;
        }

        bool IsValidSpawnPosition(Vector3 position)
        {
            float waterHeight = TerrainMeta.WaterMap.GetHeight(position);
            if (position.y <= waterHeight + MIN_HEIGHT_ABOVE_WATER)
                return false;

            Vector3[] checkPoints = new Vector3[]
            {
                position + Vector3.forward * WATER_CHECK_RADIUS,
                position + Vector3.back * WATER_CHECK_RADIUS,
                position + Vector3.left * WATER_CHECK_RADIUS,
                position + Vector3.right * WATER_CHECK_RADIUS
            };

            foreach (var point in checkPoints)
            {
                if (TerrainMeta.WaterMap.GetHeight(point) >= position.y - MIN_HEIGHT_ABOVE_WATER)
                    return false;
            }

            if (Physics.CheckSphere(position, BUILDING_CHECK_RADIUS, ConstructionMask))
                return false;

            int topology = TerrainMeta.TopologyMap.GetTopology(position);
            if ((topology & (int)(TerrainTopology.Enum.Road | TerrainTopology.Enum.Roadside | TerrainTopology.Enum.Cliff | 
                TerrainTopology.Enum.Ocean | TerrainTopology.Enum.Decor | TerrainTopology.Enum.Monument | TerrainTopology.Enum.Beach)) != 0)
                return false;

            foreach (var ore in spawnedOres)
            {
                if (ore != null && !ore.IsDestroyed && Vector3.Distance(ore.transform.position, position) < MIN_SPAWN_DISTANCE)
                    return false;
            }

            foreach (var monument in TerrainMeta.Path.Monuments)
            {
                if (monument == null) continue;
                float distance = Vector3.Distance(monument.transform.position, position);
                if (distance < monument.Bounds.size.magnitude)
                    return false;
            }

            return true;
        }

        bool SpawnOreAtPosition(string prefab, Vector3 position)
        {
            var entity = GameManager.server.CreateEntity(prefab, position);
            if (entity == null) return false;

            entity.transform.position = position;
            entity.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            
            try
            {
                entity.Spawn();
                spawnedOres.Add(entity);
                return true;
            }
            catch
            {
                if (entity != null && !entity.IsDestroyed)
                    entity.Kill();
                return false;
            }
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            var baseEntity = entity as BaseEntity;
            if (baseEntity != null && spawnedOres.Contains(baseEntity))
            {
                if (baseEntity.HasFlag(BaseEntity.Flags.Reserved8)) return;

                spawnedOres.Remove(baseEntity);
                string prefab = baseEntity.PrefabName;
                
                timer.Once(300f, () => 
                {
                    if (prefab == MetalOre)
                        SpawnSpecificOre(MetalOre, 1);
                    else if (prefab == SulfurOre)
                        SpawnSpecificOre(SulfurOre, 1);
                    else if (prefab == StoneOre)
                        SpawnSpecificOre(StoneOre, 1);
                });
            }
        }

        [ChatCommand("respawn_ores")]
        void CommandRespawnOres(BasePlayer player)
        {
            if (!player.IsAdmin) return;
            CleanupAllOres();
            SpawnOresAcrossMap();
            player.ChatMessage("Все руды были респавнены согласно ванильным настройкам!");
        }
    }
}