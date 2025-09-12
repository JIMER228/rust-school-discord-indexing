using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("AnimalSpawner", "REIN", "1.0.0")]
    [Description("Allows configurable spawning of animals across the map")]
    public class AnimalSpawner : RustPlugin
    {
        private ConfigData configData;
        private const float WorldSize = 4000f; // Стандартный размер карты Rust
        private Dictionary<string, string> animalPrefabMap;

        private class ConfigData
        {
            public Dictionary<string, AnimalConfig> Animals { get; set; }
            public float SpawnCheckInterval { get; set; }
        }

        private class AnimalConfig
        {
            public int MaxAmount { get; set; }
            public float SpawnChance { get; set; }
            public float MinSpawnDistance { get; set; }
            public float MaxSpawnDistance { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData
            {
                SpawnCheckInterval = 10f, // Уменьшаем интервал проверки до 10 секунд
                Animals = new Dictionary<string, AnimalConfig>
                {
                    ["bear"] = new AnimalConfig
                    {
                        MaxAmount = 5,
                        SpawnChance = 0.3f,
                        MinSpawnDistance = 50f,
                        MaxSpawnDistance = 200f
                    },
                    ["wolf"] = new AnimalConfig
                    {
                        MaxAmount = 8,
                        SpawnChance = 0.4f,
                        MinSpawnDistance = 50f,
                        MaxSpawnDistance = 200f
                    },
                    ["boar"] = new AnimalConfig
                    {
                        MaxAmount = 10,
                        SpawnChance = 0.5f,
                        MinSpawnDistance = 50f,
                        MaxSpawnDistance = 200f
                    },
                    ["chicken"] = new AnimalConfig
                    {
                        MaxAmount = 12,
                        SpawnChance = 0.6f,
                        MinSpawnDistance = 50f,
                        MaxSpawnDistance = 200f
                    }
                }
            };
            PrintWarning("Загружена конфигурация по умолчанию");
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(configData);
        }

        void OnServerInitialized()
        {
            try 
            {
                // Инициализируем карту префабов
                animalPrefabMap = new Dictionary<string, string>
                {
                    ["assets/rust.ai/agents/bear/bear.prefab"] = "bear",
                    ["assets/rust.ai/agents/wolf/wolf.prefab"] = "wolf",
                    ["assets/rust.ai/agents/boar/boar.prefab"] = "boar",
                    ["assets/rust.ai/agents/chicken/chicken.prefab"] = "chicken"
                };

                // Отключаем стандартный спавн
                var count = 0;
                foreach (var entity in BaseNetworkable.serverEntities.OfType<BaseAnimalNPC>())
                {
                    entity.Kill();
                    count++;
                }
                PrintWarning($"[AnimalSpawner] Удалено {count} существующих животных");

                // Сразу спавним первую партию животных
                CheckAndSpawnAnimals();

                // Устанавливаем таймер на регулярную проверку спавна
                timer.Every(configData.SpawnCheckInterval, () => {
                    CheckAndSpawnAnimals();
                });

                // Устанавливаем таймер на подсчёт животных каждые 120 секунд
                timer.Every(120f, () => {
                    var stats = new Dictionary<string, int>();
                    var totalAnimals = 0;

                    // Подсчитываем количество каждого типа животных
                    foreach (var animalKvp in configData.Animals)
                    {
                        string animalName = animalKvp.Key;
                        var count = BaseNetworkable.serverEntities.OfType<BaseAnimalNPC>()
                            .Count(x => x.ShortPrefabName.Contains(animalName));
                        stats[animalName] = count;
                        totalAnimals += count;
                    }

                    // Выводим статистику
                    PrintWarning($"[AnimalSpawner] Статистика животных на карте:");
                    foreach (var stat in stats)
                    {
                        PrintWarning($"[AnimalSpawner] {stat.Key}: {stat.Value}");
                    }
                    PrintWarning($"[AnimalSpawner] Всего животных: {totalAnimals}");
                });

                PrintWarning("[AnimalSpawner] Успешно инициализирован!");
            }
            catch (System.Exception ex)
            {
                PrintError($"[AnimalSpawner] Ошибка при инициализации плагина: {ex.Message}");
            }
        }

        object OnEntitySpawn(BaseAnimalNPC animal)
        {
            if (animal == null) return null;

            string prefabPath = animal.PrefabName;
            if (animalPrefabMap.ContainsKey(prefabPath))
            {
                // Отменяем стандартный спавн
                NextTick(() => {
                    if (animal != null && !animal.IsDestroyed)
                    {
                        animal.Kill();
                    }
                });
                return true; // Блокируем спавн
            }

            return null;
        }

        void OnEntityDeath(BaseAnimalNPC animal, HitInfo info)
        {
            if (animal == null) return;

            // Запускаем проверку спавна при смерти животного
            timer.Once(1f, () => CheckAndSpawnAnimals());
        }

        private void CheckAndSpawnAnimals()
        {
            if (configData?.Animals == null)
            {
                PrintError("Конфигурация отсутствует или повреждена!");
                return;
            }

            foreach (var animalKvp in configData.Animals)
            {
                string animalName = animalKvp.Key;
                AnimalConfig config = animalKvp.Value;

                // Подсчет текущего количества животных данного типа
                var currentAmount = BaseNetworkable.serverEntities.OfType<BaseAnimalNPC>()
                    .Count(x => x.ShortPrefabName.Contains(animalName));

                // Пытаемся создать животных, если их меньше максимума
                int attemptsLeft = config.MaxAmount - currentAmount;
                for (int i = 0; i < attemptsLeft; i++)
                {
                    float chance = UnityEngine.Random.value;
                    
                    if (chance <= config.SpawnChance)
                    {
                        if (SpawnAnimal(animalName, config))
                        {
                            currentAmount++;
                            PrintWarning($"[AnimalSpawner] Создано животное {animalName}");
                            // Небольшая задержка между спавном животных
                            if (i < attemptsLeft - 1)
                            {
                                timer.Once(0.5f, () => CheckAndSpawnAnimals());
                                return;
                            }
                        }
                    }
                }
            }
        }

        private bool SpawnAnimal(string animalName, AnimalConfig config)
        {
            // Получаем случайную позицию на карте
            Vector3 randomPos = GetRandomPosition(config);
            if (randomPos == Vector3.zero)
            {
                return false;
            }

            // Получаем правильный путь к префабу
            string prefabPath = $"assets/rust.ai/agents/{animalName}/{animalName}.prefab";
            
            // Создаем сущность с небольшой задержкой
            timer.Once(0.1f, () =>
            {
                var entity = GameManager.server.CreateEntity(prefabPath, randomPos);
                if (entity != null)
                {
                    entity.Spawn();
                }
                else
                {
                    PrintError($"[AnimalSpawner] Ошибка создания {animalName}");
                }
            });

            return true;
        }

        private Vector3 GetRandomPosition(AnimalConfig config)
        {
            var tries = 0;
            const int maxTries = 30;

            while (tries < maxTries)
            {
                var randomPoint = TerrainMeta.Position + new Vector3(
                    UnityEngine.Random.Range(-WorldSize/2f, WorldSize/2f),
                    0,
                    UnityEngine.Random.Range(-WorldSize/2f, WorldSize/2f)
                );

                float height = TerrainMeta.HeightMap.GetHeight(randomPoint);
                
                if (height <= 0f || height > 100f)
                {
                    tries++;
                    continue;
                }

                randomPoint.y = height + 0.5f;

                if (IsValidSpawnPoint(randomPoint))
                {
                    return randomPoint;
                }

                tries++;
            }

            return Vector3.zero;
        }

        private bool IsValidSpawnPoint(Vector3 position)
        {
            // Проверяем, что точка находится на земле и не в воде
            float water = TerrainMeta.WaterMap.GetHeight(position);
            float ground = TerrainMeta.HeightMap.GetHeight(position);
            
            // Упрощенные проверки
            if (position.y <= water || // Точка под водой
                position.y <= 0f || // Ниже уровня моря
                position.y > 100f) // Слишком высоко
            {
                return false;
            }

            // Проверяем наличие препятствий сверху с увеличенной дистанцией
            RaycastHit hit;
            if (GamePhysics.Trace(new Ray(position + new Vector3(0, 3f, 0), Vector3.down), 0f, out hit, 4f, LayerMask.GetMask("Terrain", "World")))
            {
                if (hit.distance > 3.5f) // Увеличенный допустимый просвет сверху
                {
                    return false;
                }
            }

            // Проверяем, нет ли построек рядом с увеличенной дистанцией
            var entities = new List<BaseEntity>();
            Vis.Entities<BaseEntity>(position, 3f, entities); // Уменьшена дистанция проверки построек
            bool hasBuildings = entities.Any(e => e is BuildingBlock || e is BuildingPrivlidge);
            
            return !hasBuildings;
        }

        [ChatCommand("animals")]
        private void CmdAnimals(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            var message = "Текущее количество животных:\n";
            foreach (var animalKvp in configData.Animals)
            {
                string animalName = animalKvp.Key;
                var count = BaseNetworkable.serverEntities.OfType<BaseAnimalNPC>()
                    .Count(x => x.ShortPrefabName.Contains(animalName));
                message += $"{animalName}: {count}/{animalKvp.Value.MaxAmount}\n";
            }

            player.ChatMessage(message);
        }
    }
}
