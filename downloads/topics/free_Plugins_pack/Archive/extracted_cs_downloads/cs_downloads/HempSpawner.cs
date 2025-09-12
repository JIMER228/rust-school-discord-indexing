using Oxide.Core;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("HempSpawner", "REIN", "1.0.0")]
    public class HempSpawner : RustPlugin
    {
        private string HEMP_PREFAB = "assets/bundled/prefabs/autospawn/collectable/hemp/hemp-collectable.prefab";
        
        private Configuration config;
        private List<BaseEntity> spawnedHemp = new List<BaseEntity>();
        
        class Configuration
        {
            [JsonProperty("Минимальное расстояние между растениями")]
            public float MinSpawnDistance = 1f;
            
            [JsonProperty("Максимальное количество растений")]
            public int MaxHempCount = 200;
            
            [JsonProperty("Интервал проверки респавна (в секундах)")]
            public float RespawnCheckTime = 300f;
            
            [JsonProperty("Процент территории карты для спавна (0.1 = 10% от размера карты)")]
            public float MapCoveragePercent = 1f;

            [JsonProperty("Высота проверки спавна")]
            public float SpawnHeight = 200f;
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new System.Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Ошибка загрузки конфигурации. Создаем новую конфигурацию.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config, true);
        }

        void Init()
        {
            LoadConfig();
            permission.RegisterPermission("hempspawner.admin", this);
        }

        void OnServerInitialized(bool initial)
        {
            ClearAllHemp();
            timer.Once(5f, () => SpawnHempCheck());
            timer.Every(config.RespawnCheckTime, () => SpawnHempCheck());
        }

        void ClearAllHemp()
        {
            foreach (var hemp in spawnedHemp.ToList())
            {
                if (hemp != null && !hemp.IsDestroyed)
                {
                    hemp.Kill();
                }
            }
            spawnedHemp.Clear();
        }

        void Unload()
        {
            ClearAllHemp();
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            var baseEntity = entity as BaseEntity;
            if (baseEntity != null && spawnedHemp.Contains(baseEntity))
            {
                spawnedHemp.Remove(baseEntity);
            }
        }

        void SpawnHempCheck()
        {
            spawnedHemp.RemoveAll(hemp => hemp == null || hemp.IsDestroyed);

            if (spawnedHemp.Count >= config.MaxHempCount) return;

            int needToSpawn = config.MaxHempCount - spawnedHemp.Count;
            int spawned = 0;
            int attempts = 0;
            int maxAttempts = needToSpawn * 2;

            while (spawned < needToSpawn && attempts < maxAttempts)
            {
                attempts++;
                if (SpawnHemp())
                    spawned++;
            }

            Puts($"Заспавнено растений: {spawned} из {needToSpawn} попыток");
        }

        bool SpawnHemp()
        {
            Vector3 randomPosition = GetRandomPosition();
            if (randomPosition == Vector3.zero) return false;

            var entity = GameManager.server.CreateEntity(HEMP_PREFAB, randomPosition);
            if (entity == null) return false;

            entity.Spawn();
            spawnedHemp.Add(entity);
            return true;
        }

        Vector3 GetRandomPosition()
        {
            float mapSize = World.Size * config.MapCoveragePercent;
            float halfMapSize = mapSize / 2f;

            for (int i = 0; i < 30; i++)
            {
                float x = UnityEngine.Random.Range(-halfMapSize, halfMapSize);
                float z = UnityEngine.Random.Range(-halfMapSize, halfMapSize);
                Vector3 randomPos = new Vector3(x, config.SpawnHeight, z);

                RaycastHit hit;
                if (Physics.Raycast(randomPos, Vector3.down, out hit, config.SpawnHeight * 2, LayerMask.GetMask("Terrain", "World", "Default")))
                {
                    Vector3 spawnPoint = hit.point + new Vector3(0, 0.1f, 0);
                    if (IsValidSpawnPosition(spawnPoint))
                    {
                        return spawnPoint;
                    }
                }
            }

            return Vector3.zero;
        }

        bool IsValidSpawnPosition(Vector3 position)
        {
            if (TerrainMeta.WaterMap.GetHeight(position) > position.y)
                return false;

            List<BaseEntity> nearbyEntities = new List<BaseEntity>();
            Vis.Entities(position, config.MinSpawnDistance, nearbyEntities);
            
            return !nearbyEntities.Any();
        }

        [Command("hemp.reload")]
        private void ReloadCommand(IPlayer player, string command, string[] args)
        {
            if (player != null && !permission.UserHasPermission(player.Id, "hempspawner.admin"))
            {
                player.Reply("У вас нет прав на использование этой команды!");
                return;
            }

            LoadConfig();
            ClearAllHemp();
            SpawnHempCheck();
            
            if (player != null)
                player.Reply("Конфигурация перезагружена и растения респавнены!");
            else
                Puts("Конфигурация перезагружена и растения респавнены!");
        }
    }
}
