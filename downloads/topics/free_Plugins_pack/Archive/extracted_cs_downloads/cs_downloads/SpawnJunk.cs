using Newtonsoft.Json;
using Oxide.Core;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("SpawnJunk", "REIN / CLASH RUST", "1.0.1")]
    class SpawnJunk : RustPlugin
    {
        private PluginConfig config;
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Благодарим за скачивание плагина на сайте RustPlugin.ru. Если вы передадите этот плагин сторонним лицам знайте - это лишает вас гарантированных обновлений!");
            config = PluginConfig.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();

            if (config.PluginVersion < Version)
                UpdateConfigValues();

            Config.WriteObject(config, true);
        }

        private void UpdateConfigValues()
        {
            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (config.PluginVersion < new VersionNumber(0, 1, 0))
            {
                PrintWarning("Config update detected! Updating config values...");
                PrintWarning("Config update completed!");
            }
            if (config.PluginVersion < new VersionNumber(1, 0, 1))
            {
                PrintWarning("Обновление конфига до версии 1.0.1");
                config.StatsInterval = 300f;
                PrintWarning("Добавлен параметр StatsInterval = 300");
            }
            config.PluginVersion = Version;
            PrintWarning("Config update completed!");
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        class PluginConfig
        {

            [JsonProperty("Максимальное количество мусорок на карте")]
            public int MaxCount = 200;

            [JsonProperty("Радиус спавна от дороги Min")]
            public int MinSpawn = 15;

            [JsonProperty("Радиус спавна от дороги Max")]
            public int MaxSpawn = 25;

            [JsonProperty("Время респавна мусорки после удаления в секундах")]
            public int RespawnTime = 120;

            [JsonProperty("Список префабов мосурок")]
            public List<string> JunkShortnamesList = new List<string>();

            [JsonProperty("Версия конфигурации")]
            public VersionNumber PluginVersion = new VersionNumber();

            [JsonProperty("Интервал проверки мусора (в секундах)")]
            public float CheckInterval = 300f;

            [JsonProperty("Спавнить мусор даже если нет игроков")]
            public bool SpawnWithoutPlayers = true;

            [JsonProperty("Максимальное количество ученых у мусорок")]
            public int MaxJunkScientists = 15;

            [JsonProperty("Включить очистку ученых")]
            public bool EnableScientistCleaning = true;

            [JsonProperty("Интервал проверки ученых (в секундах)")]
            public float ScientistCheckInterval = 300f;

            [JsonProperty("Интервал вывода статистики (в секундах)")]
            public float StatsInterval = 300f;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    PluginVersion = new VersionNumber(),
                    JunkShortnamesList = new List<string>()
                      {
                           "assets/prefabs/misc/junkpile/junkpile_a.prefab",
                            "assets/prefabs/misc/junkpile/junkpile_b.prefab",
                            "assets/prefabs/misc/junkpile/junkpile_c.prefab",
                            "assets/prefabs/misc/junkpile/junkpile_d.prefab",
                            "assets/prefabs/misc/junkpile/junkpile_f.prefab",
                            "assets/prefabs/misc/junkpile/junkpile_e.prefab",
                            "assets/prefabs/misc/junkpile/junkpile_g.prefab",
                            "assets/prefabs/misc/junkpile/junkpile_i.prefab",
                            "assets/prefabs/misc/junkpile/junkpile_j.prefab"
                     },
                    MinSpawn = 15,
                    MaxSpawn = 25,
                    MaxCount = 200,
                    RespawnTime = 120,
                    CheckInterval = 300f,
                    SpawnWithoutPlayers = true,
                    MaxJunkScientists = 15,
                    EnableScientistCleaning = true,
                    ScientistCheckInterval = 300f,
                    StatsInterval = 300f
                };
            }
        }

        int terrainMask = LayerMask.GetMask("Terrain");
        List<Vector3> Positions = new List<Vector3>();
        private static int blockedLayer = LayerMask.GetMask("Default");
        public Dictionary<JunkPile, Vector3> JunkList = new Dictionary<JunkPile, Vector3>();

        static float GetGroundPosition(Vector3 pos)
        {
            float y = TerrainMeta.HeightMap.GetHeight(pos);
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 20f, pos.z), Vector3.down, out hit, Mathf.Infinity, LayerMask.GetMask(new[]
            {
                "Terrain", "World", "Default", "Construction", "Deployed"
            }
            )) && !hit.collider.name.Contains("rock_cliff")) return Mathf.Max(hit.point.y, y);
            return y;
        }

        static Vector3 RandomCircle(Vector3 center, float radius = 2)
        {
            float ang = UnityEngine.Random.value * 360;
            Vector3 pos = Vector3.zero;
            pos.x = center.x + radius * Mathf.Sin(ang * Mathf.Deg2Rad);
            pos.z = center.z + radius * Mathf.Cos(ang * Mathf.Deg2Rad);
            pos.y = GetGroundPosition(pos);
            return pos;
        }

        void OnServerInitialized()
        {
            Subscribe("OnEntityKill");
            
            // Генерируем позиции для спавна
            GeneratePositions();
            
            // Первичный спавн мусора
            SpawnableJunk();
            
            Puts("SpawnJunk загружен! Проверка мусора каждые {0} секунд", config.CheckInterval);
            // Запускаем периодическую проверку
            timer.Every(config.CheckInterval, CheckAndRespawnJunk);

            // Запускаем проверку ученых если включено
            if (config.EnableScientistCleaning)
            {
                timer.Every(config.ScientistCheckInterval, CleanJunkScientists);
                Puts("Очистка ученых активна! Проверка каждые {0} секунд", config.ScientistCheckInterval);
            }

            // Запускаем периодический вывод статистики
            timer.Every(config.StatsInterval, PrintScientistStats);
            Puts($"Вывод статистики активен! Интервал: {config.StatsInterval} секунд");
        }

        private void GeneratePositions()
        {
            Positions.Clear();
            int count1 = 0;

            var roads = TerrainMeta.Path.Roads;
            foreach (var generate in roads)
            {
                foreach (var pos in generate.Path.Points)
                {
                    var pos1 = pos;
                    for (int i = 0; i < 4; i++)
                    {
                        var pos2 = new Vector3();
                        switch (i)
                        {
                            case 0:
                                pos2 = pos1 + new Vector3(-2, 0, UnityEngine.Random.Range(config.MinSpawn, config.MaxSpawn));
                                break;
                            case 1:
                                pos2 = pos1 + new Vector3(UnityEngine.Random.Range(config.MinSpawn, config.MaxSpawn), 0, 2);

                                break;
                            case 2:
                                pos2 = pos1 + new Vector3(-UnityEngine.Random.Range(config.MinSpawn, config.MaxSpawn), 0, 2);
                                break;
                            case 3:
                                pos2 = pos1 + new Vector3(-2, 0, -UnityEngine.Random.Range(config.MinSpawn, config.MaxSpawn));
                                break;
                        }

                        pos2.y = GetGroundPosition(pos2);

                        if ((pos2.y - pos.y) > 2 || (pos2.y - pos.y) < -2)
                        {
                            continue;
                        }
                        var count = Physics.OverlapSphereNonAlloc(pos2, 15, Vis.colBuffer, blockedLayer);

                        if (count > 0)
                            break;

                        RaycastHit hit;

                        if (Physics.Raycast(pos2 + Vector3.up, Vector3.down, out hit, terrainMask))
                        {
                            if (hit.GetCollider().name.Contains("Terrain"))
                            {
                                Positions.Add(pos2);
                            }
                            else
                            {
                                pos2.x = pos2.x + 3f;
                                if (Physics.Raycast(pos2 + Vector3.up, Vector3.down, out hit, terrainMask))
                                {
                                    if (hit.GetCollider().name.Contains("Terrain"))
                                    {
                                        Positions.Add(pos2);
                                    }
                                }
                            }
                        }
                        count1++;
                    }
                }

            }
            Puts("Сгенерировано {0} позиций для спавна мусора", Positions.Count);
        }

        void SpawnableJunk()
        {
            int Spawned = 0;
            for (int i = 0; i < config.MaxCount; i++)
            {
                if (Positions.Count == 0) break;
                var en = Positions.GetRandom();
                if (en == Vector3.zero)
                {
                    i--;
                    continue;
                }
                var count = Physics.OverlapSphereNonAlloc(en, 15, Vis.colBuffer, blockedLayer);
                if (count > 0)
                {
                    i--;
                    continue;
                }
                var pos = en;

                var entity = GameManager.server.CreateEntity(config.JunkShortnamesList.GetRandom(), pos + new Vector3(0, 0.2f, 0)) as JunkPile;
                if (entity == null)
                {
                    i--;
                    entity.Kill();
                    continue;
                }
                entity.enableSaving = false;
                entity.Spawn();
                JunkList.Add(entity, pos);
                Positions.Remove(en);
                Spawned++;
            }
            if (Spawned > 0)
            {
                Puts("Заспавнено {0} новых мусорок", Spawned);
            }
        }

        void CheckAndRespawnJunk()
        {
            if (!config.SpawnWithoutPlayers && BasePlayer.activePlayerList.Count == 0)
            {
                Puts("Пропуск спавна мусора: нет активных игроков");
                return;
            }

            int currentCount = JunkList.Count;
            
            // Проверяем на null объекты и удаляем их
            var invalidJunk = JunkList.ToList().Where(x => x.Key == null || x.Key.IsDestroyed).ToList();
            foreach(var pair in invalidJunk)
            {
                JunkList.Remove(pair.Key);
            }
            
            if (invalidJunk.Count > 0)
            {
                Puts($"Удалено {invalidJunk.Count} недействительных мусорных объектов");
                currentCount = JunkList.Count;
            }
            
            if (currentCount < config.MaxCount)
            {
                int needToSpawn = config.MaxCount - currentCount;
                int spawned = 0;
                
                for (int i = 0; i < needToSpawn; i++)
                {
                    if (Positions.Count == 0) break;

                    var randomPosition = Positions.GetRandom();
                    if (randomPosition == Vector3.zero)
                        continue;

                    var count = Physics.OverlapSphereNonAlloc(randomPosition, 15, Vis.colBuffer, blockedLayer);
                    if (count > 0)
                        continue;

                    var prefab = config.JunkShortnamesList.GetRandom();
                    var junk = GameManager.server.CreateEntity(prefab, randomPosition) as JunkPile;
                    if (junk != null)
                    {
                        junk.Spawn();
                        JunkList[junk] = randomPosition;
                        spawned++;
                    }
                }
                
                if (spawned > 0)
                {
                    Puts("Авто-респавн: добавлено {0} мусорок (всего {1}/{2})", spawned, JunkList.Count, config.MaxCount);
                }
            }
        }

        private void CleanJunkScientists()
        {
            if (!config.EnableScientistCleaning) return;

            var entities = BaseNetworkable.serverEntities.ToList();
            var junkScientists = new List<ScientistNPC>();
            int totalFound = 0;

            foreach (var entity in entities)
            {
                var scientist = entity as ScientistNPC;
                if (scientist == null) continue;

                // Проверяем только ученых у мусорок
                if (!scientist.name.Contains("junkpile")) continue;

                totalFound++;
                junkScientists.Add(scientist);
            }

            if (junkScientists.Count <= config.MaxJunkScientists)
            {
                Puts($"Найдено ученых у мусорок: {totalFound}. Не превышает лимит {config.MaxJunkScientists}, пропускаем очистку.");
                return;
            }

            int toRemove = junkScientists.Count - config.MaxJunkScientists;
            int removed = 0;

            // Удаляем лишних ученых
            for (int i = 0; i < toRemove; i++)
            {
                if (junkScientists[i] != null && !junkScientists[i].IsDestroyed)
                {
                    junkScientists[i].Kill();
                    removed++;
                }
            }

            Puts($"Очистка ученых: было {totalFound}, удалено {removed}, осталось {junkScientists.Count - removed}");
        }

        private void PrintScientistStats()
        {
            var entities = BaseNetworkable.serverEntities.ToList();
            int totalScientists = 0;
            int junkpileScientists = 0;

            foreach (var entity in entities)
            {
                var scientist = entity as ScientistNPC;
                if (scientist == null) continue;

                totalScientists++;
                if (scientist.name.Contains("junkpile"))
                    junkpileScientists++;
            }

            PrintWarning("=== Статистика по ученым ===");
            PrintWarning($"Всего ученых на карте: {totalScientists}");
            PrintWarning($"Ученых у мусорок: {junkpileScientists}");
            PrintWarning($"Максимальный лимит у мусорок: {config.MaxJunkScientists}");
            PrintWarning("==========================");
        }

        void Unload()
        {
            Unsubscribe("OnEntityKill");
            foreach (var ore in JunkList)
            {
                if (ore.Key != null && !ore.Key.IsDestroyed)
                {
                    ore.Key.Kill();
                    var count = Physics.OverlapSphereNonAlloc(ore.Value, 10, Vis.colBuffer, blockedLayer);

                    for (int i = 0; i < count; i++)
                    {
                        var entity = Vis.colBuffer[i].ToBaseEntity();
                        if (entity == null) continue;
                        entity.Kill();
                    }
                }
            }
        }

        void OnEntityKill(JunkPile entity)
        {
            if (JunkList.ContainsKey(entity))
            {
                var pos = JunkList[entity];
                Puts("Мусорка уничтожена. Респавн через {0} секунд. Осталось: {1}/{2}", config.RespawnTime, JunkList.Count - 1, config.MaxCount);
                
                timer.Once(config.RespawnTime, () =>
                {
                    var newEntity = GameManager.server.CreateEntity(config.JunkShortnamesList.GetRandom(), pos) as JunkPile;
                    if (newEntity == null)
                    {
                        newEntity.Kill();
                        return;
                    }
                    newEntity.enableSaving = false;
                    newEntity.Spawn();
                    JunkList.Add(entity, pos);
                });
                JunkList.Remove(entity);
            }
        }

        [ConsoleCommand("spawnjunk_list")]
        void cmdListSpawnOres(ConsoleSystem.Arg args)
        {

            if (args.Connection != null) return;
            PrintWarning($"{JunkList.Count} junk to map");
        }

        [ConsoleCommand("spawnjunk.scientists")]
        void cmdCheckScientists(ConsoleSystem.Arg args)
        {
            if (args.Connection != null && !args.IsAdmin) return;
            PrintScientistStats();
        }

        [ChatCommand("cleanscientists")]
        private void CmdCleanScientists(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;
            
            CleanJunkScientists();
            SendReply(player, "Запущена очистка ученых у мусорок");
        }
    }
}
