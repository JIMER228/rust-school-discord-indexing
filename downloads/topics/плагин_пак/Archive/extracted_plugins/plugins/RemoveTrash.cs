using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("RemoveTrash", "Инкуб/WOLF SPIRIT", "1.3.4")]
    class RemoveTrash : RustPlugin
    {
        private Configuration config;
        private bool logUnrecognizedObjects = true; // Флаг для логирования нераспознанных объектов - включен по умолчанию
        private Timer cleanupTimer; // Явный таймер для лучшего контроля

        // Метод для проверки нахождения объекта в защищенной зоне
        private bool IsInProtectedZone(Vector3 position)
        {
            float distance = Vector3.Distance(position, Vector3.zero);
            return distance < 50f; // Радиус зоны защиты
        }

        // Универсальный метод для безопасного поиска объектов
        private List<BaseEntity> SafeFindEntities(Vector3 center, float radius, string operationName = "operation")
        {
            var entities = new List<BaseEntity>();
            float searchRadius = Math.Min(radius, 40f); // Максимальный радиус поиска 40м
            
            try
            {
                Vis.Entities(center, searchRadius, entities);
                LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {operationName} - Found {entities.Count} entities in radius {searchRadius}m", this);
            }
            catch (Exception ex)
            {
                LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {operationName} - Vis.Entities failed with searchRadius {searchRadius}: {ex.Message}", this);
                // Пробуем с меньшим радиусом
                searchRadius = Math.Min(radius, 40f);
                entities.Clear();
                try
                {
                    Vis.Entities(center, searchRadius, entities);
                    LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {operationName} - Retrying with reduced searchRadius: {searchRadius}m, found {entities.Count} entities", this);
                }
                catch (Exception ex2)
                {
                    LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {operationName} - Second attempt failed: {ex2.Message}", this);
                }
            }
            
            return entities;
        }

        // Метод для проверки, является ли объект кодовым замком
        private bool IsCodeLock(BaseEntity entity)
        {
            if (entity == null) return false;
            
            string entityName = entity.ShortPrefabName.ToLower();
            
            // Проверяем по именам префабов
            string[] lockPrefabs = {
                "lock.code", "lock.code.deployed", "lock.keypad", "lock.keypad.deployed",
                "codelock", "codelock.deployed", "keypad", "keypad.deployed",
                "keypadlock", "keypadlock.deployed", "keypad_lock", "keypad_lock.deployed"
            };
            
            foreach (var prefab in lockPrefabs)
            {
                if (entityName.Contains(prefab.ToLower()))
                    return true;
            }
            
            // Проверяем компоненты (только доступные)
            try
            {
                if (entity.GetComponent<CodeLock>() != null || 
                    entity.GetComponent<KeyLock>() != null)
                    return true;
            }
            catch
            {
                // Игнорируем ошибки доступа к компонентам
            }
            
            return false;
        }

        class Configuration
        {
            public string Version { get; set; } = "1.3.4";
            public float DistanceRadius { get; set; } = 50f;
            public float IntervalCheck { get; set; } = 60f; // Уменьшил интервал до 60 секунд
            public Position PositionDeletions { get; set; } = new Position();
            public string[] Exclude { get; set; } = new string[] { 
            // Только самые важные исключения
            "player_corpse",
            "stash.small",
            "ammo.nailgun.nails",
            "box.wooden",
            "box.wooden.large",
            "rowboat_storage",
            "workbench1.deployed",
            "workbench2.deployed",
            "workbench3.deployed",
            "pager.entity",
            "workcart_fuel_storage"
            };
            public bool DeleteNPC { get; set; } = false;
            public bool DeletePlayers { get; set; } = false;
        }

        class Position
        {
            public float x { get; set; } = 0f;
            public float y { get; set; } = 0f;
            public float z { get; set; } = 0f;
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            config = new Configuration();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null || config.Version != Version.ToString())
                {
                    PrintWarning("The configuration file does not match the current version, update...");
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintWarning("Configuration file is corrupted, create a new one");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);

        [ChatCommand("removetrash")]
        private void RemoveTrashCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            if (args.Length == 0)
            {
                player.ChatMessage("RemoveTrash commands:\n" +
                    "/removetrash log on - Enable logging of unrecognized objects\n" +
                    "/removetrash log off - Disable logging of unrecognized objects\n" +
                    "/removetrash clean - Force cleanup now\n" +
                    "/removetrash canisters - Force cleanup of canisters only\n" +
                    "/removetrash locks - Force cleanup of code locks only\n" +
                    "/removetrash status - Show current status\n" +
                    "/removetrash restart - Restart the cleanup timer\n" +
                    "/removetrash testlogs - Test log file creation\n" +
                    "/removetrash scan - Scan and log all objects in area\n" +
                    "/removetrash force - Force delete ALL objects in area (DANGEROUS!)\n" +
                    "/removetrash nuke - NUKE problematic areas with massive object spam (EXTREME!)\n" +
                    "/removetrash zone - Clean protected zone (zero point) only");
                return;
            }

            switch (args[0].ToLower())
            {
                case "log":
                    if (args.Length > 1)
                    {
                        logUnrecognizedObjects = args[1].ToLower() == "on";
                        player.ChatMessage($"Unrecognized objects logging: {(logUnrecognizedObjects ? "ENABLED" : "DISABLED")}");
                    }
                    break;
                case "clean":
                    RemoveObjects();
                    player.ChatMessage("Forced cleanup completed.");
                    break;
                case "canisters":
                    RemoveCanistersOnly();
                    player.ChatMessage("Canister cleanup completed.");
                    break;
                case "locks":
                    RemoveCodeLocksOnly();
                    player.ChatMessage("Code locks cleanup completed.");
                    break;
                case "scan":
                    ScanAllObjects();
                    player.ChatMessage("Area scan completed. Check logs for details.");
                    break;
                case "force":
                    ForceDeleteAll();
                    player.ChatMessage("Force deletion completed. Check logs for details.");
                    break;
                case "nuke":
                    NukeProblematicArea();
                    player.ChatMessage("NUKE operation completed. Check logs for details.");
                    break;
                case "zone":
                    CleanProtectedZone();
                    player.ChatMessage("Protected zone cleanup completed. Check logs for details.");
                    break;
                case "testlogs":
                    LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Test log entry from command", this);
                    LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Test error log entry", this);
                    LogToFile("unrecognized_objects.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Test unrecognized objects log entry", this);
                    player.ChatMessage("Test log entries created. Check oxide/logs/ folder.");
                    break;
                case "status":
                    player.ChatMessage($"RemoveTrash Status:\n" +
                        $"Logging unrecognized objects: {(logUnrecognizedObjects ? "ON" : "OFF")}\n" +
                        $"Interval: {config.IntervalCheck}s\n" +
                        $"Radius: {config.DistanceRadius}m\n" +
                        $"Position: ({config.PositionDeletions.x}, {config.PositionDeletions.y}, {config.PositionDeletions.z})\n" +
                        $"Timer active: {(cleanupTimer != null ? "YES" : "NO")}");
                    break;
                case "restart":
                    if (cleanupTimer != null)
                        cleanupTimer.Destroy();
                    cleanupTimer = timer.Every(config.IntervalCheck, RemoveObjects);
                    player.ChatMessage("Cleanup timer restarted.");
                    break;
            }
        }

        private void OnServerInitialized()
        {
            PrintWarning("\n-----------------------------------------------------------------------------------------\n" +
            "     Loading plugin...\n" +
            "     oxide-russia.ru...\n" +
            "     Enjoy your use!....\n" +
            "-----------------------------------------------------------------------------------------");
            
            // Создаем явный таймер для лучшего контроля
            cleanupTimer = timer.Every(config.IntervalCheck, RemoveObjects);
            
            // Первоначальная очистка через 10 секунд после загрузки
            timer.Once(10f, RemoveObjects);
            
            // Создаем тестовый лог для проверки работы логирования
            LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: RemoveTrash plugin loaded successfully. Version: {Version}", this);
            LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Error log initialized", this);
            LogToFile("unrecognized_objects.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Unrecognized objects log initialized", this);
            
            PrintWarning("RemoveTrash plugin loaded. Log files created. Use /removetrash for commands.");
        }

        private void RemoveObjects()
        {
            try
            {
                Vector3 center = new Vector3(config.PositionDeletions.x, config.PositionDeletions.y, config.PositionDeletions.z);
                float radius = config.DistanceRadius;
                int removedCount = 0;
                HashSet<string> exclusionSet = new HashSet<string>(config.Exclude);
                
                // Логируем начало процесса очистки
                LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Starting cleanup process. Center: ({center.x}, {center.y}, {center.z}), Radius: {radius}m", this);

                // Список объектов для принудительного удаления
                string[] forceDeleteKeywords = new string[] {
                    // Канистры - расширенный список
                    "jerrycan", "gascan", "fuelcan", "oilcan", "redcangassmall", "redcangaslarge", 
                    "bluecangassmall", "bluecangaslarge", "waterjug", "watercan", "waterbottle",
                    "fuel", "gas", "oil", "water", "can", "jug", "bottle", "container",
                    "fuel_barrel", "fuel_barrel.deployed", "fuel_barrel_empty", "fuel_barrel_empty.deployed",
                    "small.oil.refinery", "small.oil.refinery.deployed", "oil_refinery", "oil_refinery.deployed",
                    "water_catcher", "water_catcher.deployed", "water_catcher_small", "water_catcher_small.deployed",
                    "water_catcher_large", "water_catcher_large.deployed", "water_catcher_wood", "water_catcher_wood.deployed",
                    "water_barrel", "water_barrel.deployed", "water_barrel_empty", "water_barrel_empty.deployed",
                    "water_bucket", "water_bucket.deployed", "water_bucket_empty", "water_bucket_empty.deployed",
                    "water_jug", "water_jug.deployed", "water_jug_empty", "water_jug_empty.deployed",
                    "water_container", "water_container.deployed", "water_container_empty", "water_container_empty.deployed",
                    "storage", "tank", "drum", "cask", "vessel", "reservoir", "cistern", "canister",
                    "fuel_storage", "water_storage", "oil_storage", "gas_storage",
                    // Бумбоксы
                    "boombox", "radio", "speaker", "music", "audio", "sound", "stereo", "jukebox",
                    // Световые эффекты
                    "light", "glow", "flare", "spark", "fire", "smoke", "particle", "vfx", "fx", "decal",
                    // Невидимые объекты
                    "invisible", "elite", "orange", "yellow", "barrier", "trigger", "collider"
                };

                // Используем универсальный метод для безопасного поиска объектов
                var entities = SafeFindEntities(center, radius, "Main cleanup");

                // Автоматическое обнаружение проблемных зон
                if (entities.Count > 5000)
                {
                    LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: WARNING: High object count detected! {entities.Count} objects found. This may cause lag.", this);
                    PrintWarning($"HIGH OBJECT COUNT DETECTED: {entities.Count} objects! Consider using /removetrash nuke for massive cleanup.");
                }

                // Пакетная обработка для предотвращения лагов
                int batchSize = 0;
                int maxBatchSize = 100; // Максимум 100 объектов за раз
                int processedCount = 0;

                foreach (var entity in entities)
                {
                    try
                    {
                        if (entity == null || entity.IsDestroyed)
                            continue;
                        if (entity is BasePlayer)
                            continue;

                        string entityName = entity.ShortPrefabName;
                        Vector3 entityPos = entity.transform.position;
                        float distance = Vector3.Distance(center, entityPos);
                        
                        // Специальная проверка для подземных объектов (y < -100)
                        bool isUnderground = entityPos.y < -100f;
                        bool isNearZero = Math.Abs(entityPos.x) < radius && Math.Abs(entityPos.z) < radius;
                        
                        // Удаляем объекты если они под землей и рядом с нулевыми координатами, или в обычном радиусе
                        if (!isUnderground && distance > radius)
                            continue;
                        if (isUnderground && !isNearZero)
                            continue;

                        string lowerName = entityName.ToLower();
                        bool shouldDelete = false;

                        // Проверяем исключения
                        if (exclusionSet.Contains(entityName))
                            continue;

                        // Проверяем NPC и игроков
                        if ((entity is NPCPlayer && !config.DeleteNPC) || 
                            (entity is BasePlayer && !config.DeletePlayers))
                            continue;

                        // Принудительное удаление по ключевым словам
                        foreach (var keyword in forceDeleteKeywords)
                        {
                            if (lowerName.Contains(keyword))
                            {
                                shouldDelete = true;
                                break;
                            }
                        }

                        // Специальная проверка для кодовых замков
                        if (!shouldDelete && IsCodeLock(entity))
                        {
                            shouldDelete = true;
                            LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Code lock detected: {entityName}", this);
                        }

                        // Расширенный список ключевых слов и префабов для светильников и источников света
                        string[] lightKeywords = new string[] {
                            // Префабы и ключевые слова для всех типов светильников и источников света
                            "ceilinglight", "ceilinglight.deployed", "simplelight", "simplelight.deployed", "searchlight", "searchlight.deployed",
                            "lantern", "lantern.deployed", "candle", "candle.deployed", "campfire", "campfire.deployed", "fireplace", "fireplace.deployed",
                            "fogmachine", "fogmachine.deployed", "disco", "disco.deployed", "strobe", "strobe.deployed", "laser", "laser.deployed",
                            "neon", "neon.deployed", "lamp", "lamp.deployed", "light", "light source", "sirenlight", "flasherlight", "industrial.wall.lamp",
                            "jackolantern", "skullspikes.candles", "skull_fire_pit", "cursedcauldron", "electricfurnace", "oven", "baseoven", "basefuellightsource",
                            // Общие ключевые слова
                            "light", "glow", "flare", "spark", "fire", "smoke", "particle", "vfx", "fx", "decal"
                        };
                        foreach (var keyword in lightKeywords)
                        {
                            if (lowerName.Contains(keyword))
                            {
                                shouldDelete = true;
                                break;
                            }
                        }

                        // Дополнительная проверка для канистр и топливных объектов
                        if (!shouldDelete)
                        {
                            // Проверяем компоненты объекта на наличие топливных свойств
                            try
                            {
                                if (entity.GetComponent<StorageContainer>() != null || 
                                    entity.GetComponent<LiquidContainer>() != null ||
                                    entity.GetComponent<FuelGenerator>() != null ||
                                    entity.GetComponent<BaseFuelLightSource>() != null)
                                {
                                    shouldDelete = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                // Игнорируем ошибки доступа к компонентам
                                if (logUnrecognizedObjects)
                                {
                                    LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Error checking components for {entityName}: {ex.Message}", this);
                                }
                            }
                        }

                        // Дополнительная проверка для невидимых объектов
                        if (!shouldDelete && entity.gameObject != null)
                        {
                            try
                            {
                                string layerName = LayerMask.LayerToName(entity.gameObject.layer).ToLower();
                                string tagName = entity.gameObject.tag.ToLower();
                                
                                // Удаляем объекты с нестандартными слоями и тегами
                                if (layerName.Contains("invisible") || layerName.Contains("ignore") || layerName.Contains("hidden") || 
                                    layerName.Contains("trigger") || layerName.Contains("collider") || layerName.Contains("barrier") ||
                                    tagName.Contains("invisible") || tagName.Contains("ignore") || tagName.Contains("hidden") ||
                                    tagName.Contains("trigger") || tagName.Contains("collider") || tagName.Contains("barrier") ||
                                    tagName.Contains("untagged") || tagName.Contains("nodelete") || tagName.Contains("protected"))
                                {
                                    shouldDelete = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                // Игнорируем ошибки доступа к gameObject
                                if (logUnrecognizedObjects)
                                {
                                    LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Error accessing GameObject for {entityName}: {ex.Message}", this);
                                }
                            }
                        }

                        // Проверка защищенной зоны - приоритетное удаление объектов в нулевой точке
                        if (IsInProtectedZone(entityPos))
                        {
                            shouldDelete = true;
                            LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Object in protected zone detected: {entityName} at ({entityPos.x:F1}, {entityPos.y:F1}, {entityPos.z:F1})", this);
                        }

                        // Временное логирование нераспознанных объектов для анализа
                        if (!shouldDelete && logUnrecognizedObjects)
                        {
                            try
                            {
                                string layerInfo = entity.gameObject != null ? $"Layer: {LayerMask.LayerToName(entity.gameObject.layer)}, Tag: {entity.gameObject.tag}" : "No GameObject";
                                LogToFile("unrecognized_objects.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Unrecognized object: {entityName} at ({entityPos.x:F1}, {entityPos.y:F1}, {entityPos.z:F1}) - {layerInfo}", this);
                            }
                            catch (Exception ex)
                            {
                                LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Error logging unrecognized object {entityName}: {ex.Message}", this);
                            }
                        }

                        if (shouldDelete)
                        {
                            try
                            {
                                entity.Kill();
                                removedCount++;
                                batchSize++;
                                processedCount++;
                                
                                string positionInfo = isUnderground ? $"underground at ({entityPos.x:F1}, {entityPos.y:F1}, {entityPos.z:F1})" : $"at distance {distance:F1}m";
                                string removalMessage = $"Deleted {entityName} {positionInfo} from center position ({config.PositionDeletions.x}, {config.PositionDeletions.y}, {config.PositionDeletions.z})";
                                
                                // Логируем только каждые 100 удалений для экономии места
                                if (removedCount % 100 == 0)
                                {
                                    PrintWarning($"Deleted {removedCount} objects so far...");
                                }
                                LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {removalMessage}", this);
                                
                                // Ограничиваем размер пакета для предотвращения лагов
                                if (batchSize >= maxBatchSize)
                                {
                                    LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Batch limit reached ({maxBatchSize}). Pausing for performance.", this);
                                    batchSize = 0;
                                    // Небольшая пауза для стабилизации производительности
                                    // Используем NextTick вместо Thread.Sleep для неблокирующей паузы
                                    NextTick(() => { });
                                }
                            }
                            catch (Exception ex)
                            {
                                LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Error deleting {entityName}: {ex.Message}", this);
                            }
                        }
                        else
                        {
                            processedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Обработка ошибок для каждого объекта
                        LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Error processing entity: {ex.Message}", this);
                        continue;
                    }
                }

                if (removedCount > 0)
                {
                    string logMessage = $"Deleted {removedCount} objects within {config.DistanceRadius}m of position {config.PositionDeletions.x}, {config.PositionDeletions.y}, {config.PositionDeletions.z}. Processed {processedCount} total entities.";
                    PrintWarning(logMessage);
                    LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {logMessage}", this);
                }
                else
                {
                    LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: No objects deleted. Processed {processedCount} total entities.", this);
                }
            }
            catch (Exception ex)
            {
                // Общая обработка ошибок
                string errorMessage = $"Critical error in RemoveObjects: {ex.Message}";
                PrintError(errorMessage);
                LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {errorMessage}\nStackTrace: {ex.StackTrace}", this);
            }
        }

        private void RemoveCodeLocksOnly()
        {
            try
            {
                Vector3 center = new Vector3(config.PositionDeletions.x, config.PositionDeletions.y, config.PositionDeletions.z);
                float radius = config.DistanceRadius;
                int removedCount = 0;

                LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Starting code locks cleanup. Center: ({center.x}, {center.y}, {center.z}), Radius: {radius}m", this);

                // Ключевые слова для кодовых замков (дополнительные)
                string[] lockKeywords = new string[] {
                    "code", "lock", "key", "pad", "combination", "pin", "password", "security"
                };

                var entities = SafeFindEntities(center, radius, "Code locks cleanup");

                int batchSize = 0;
                int maxBatchSize = 100; // Максимум 100 объектов за раз
                int processedCount = 0;

                foreach (var entity in entities)
                {
                    try
                    {
                        if (entity == null || entity.IsDestroyed)
                            continue;
                        if (entity is BasePlayer)
                            continue;

                        string entityName = entity.ShortPrefabName;
                        Vector3 entityPos = entity.transform.position;
                        float distance = Vector3.Distance(center, entityPos);
                        
                        bool isUnderground = entityPos.y < -100f;
                        bool isNearZero = Math.Abs(entityPos.x) < radius && Math.Abs(entityPos.z) < radius;
                        
                        if (!isUnderground && distance > radius)
                            continue;
                        if (isUnderground && !isNearZero)
                            continue;

                        string lowerName = entityName.ToLower();
                        bool shouldDelete = false;

                        // Проверяем ключевые слова
                        foreach (var keyword in lockKeywords)
                        {
                            if (lowerName.Contains(keyword))
                            {
                                shouldDelete = true;
                                break;
                            }
                        }

                        // Проверяем кодовые замки
                        if (!shouldDelete && IsCodeLock(entity))
                        {
                            shouldDelete = true;
                        }

                        if (shouldDelete)
                        {
                            try
                            {
                                entity.Kill();
                                removedCount++;
                                batchSize++;
                                processedCount++;
                                
                                string positionInfo = isUnderground ? $"underground at ({entityPos.x:F1}, {entityPos.y:F1}, {entityPos.z:F1})" : $"at distance {distance:F1}m";
                                string removalMessage = $"Deleted code lock {entityName} {positionInfo}";
                                
                                PrintWarning(removalMessage);
                                LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {removalMessage}", this);
                                
                                if (batchSize >= maxBatchSize)
                                {
                                    batchSize = 0;
                                    // Используем NextTick вместо Thread.Sleep для неблокирующей паузы
                                    NextTick(() => { });
                                }
                            }
                            catch (Exception ex)
                            {
                                LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Error deleting code lock {entityName}: {ex.Message}", this);
                            }
                        }
                        else
                        {
                            processedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Error processing entity for code locks: {ex.Message}", this);
                    }
                }

                if (removedCount > 0)
                {
                    string logMessage = $"Deleted {removedCount} code locks within {config.DistanceRadius}m of position {config.PositionDeletions.x}, {config.PositionDeletions.y}, {config.PositionDeletions.z}. Processed {processedCount} total entities.";
                    PrintWarning(logMessage);
                    LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {logMessage}", this);
                }
                else
                {
                    LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: No code locks deleted. Processed {processedCount} total entities.", this);
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Critical error in RemoveCodeLocksOnly: {ex.Message}";
                PrintError(errorMessage);
                LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {errorMessage}\nStackTrace: {ex.StackTrace}", this);
            }
        }

        private void RemoveCanistersOnly()
        {
            try
            {
                Vector3 center = new Vector3(config.PositionDeletions.x, config.PositionDeletions.y, config.PositionDeletions.z);
                float radius = config.DistanceRadius;
                int removedCount = 0;

                // Специальные ключевые слова для канистр
                string[] canisterKeywords = new string[] {
                    "jerrycan", "gascan", "fuelcan", "oilcan", "redcangassmall", "redcangaslarge", 
                    "bluecangassmall", "bluecangaslarge", "waterjug", "watercan", "waterbottle",
                    "fuel", "gas", "oil", "water", "can", "jug", "bottle", "container",
                    "fuel_barrel", "fuel_barrel.deployed", "fuel_barrel_empty", "fuel_barrel_empty.deployed",
                    "small.oil.refinery", "small.oil.refinery.deployed", "oil_refinery", "oil_refinery.deployed",
                    "water_catcher", "water_catcher.deployed", "water_catcher_small", "water_catcher_small.deployed",
                    "water_catcher_large", "water_catcher_large.deployed", "water_catcher_wood", "water_catcher_wood.deployed",
                    "water_barrel", "water_barrel.deployed", "water_barrel_empty", "water_barrel_empty.deployed",
                    "water_bucket", "water_bucket.deployed", "water_bucket_empty", "water_bucket_empty.deployed",
                    "water_jug", "water_jug.deployed", "water_jug_empty", "water_jug_empty.deployed",
                    "water_container", "water_container.deployed", "water_container_empty", "water_container_empty.deployed"
                };

                var entities = SafeFindEntities(center, radius, "Canisters cleanup");

                int batchSize = 0;
                int maxBatchSize = 100; // Максимум 100 объектов за раз
                int processedCount = 0;
                
                foreach (var entity in entities)
                {
                    try
                    {
                        if (entity == null || entity.IsDestroyed)
                            continue;
                        if (entity is BasePlayer)
                            continue;

                        string entityName = entity.ShortPrefabName;
                        Vector3 entityPos = entity.transform.position;
                        float distance = Vector3.Distance(center, entityPos);
                        
                        // Специальная проверка для подземных объектов (y < -100)
                        bool isUnderground = entityPos.y < -100f;
                        bool isNearZero = Math.Abs(entityPos.x) < radius && Math.Abs(entityPos.z) < radius;
                        
                        // Удаляем объекты если они под землей и рядом с нулевыми координатами, или в обычном радиусе
                        if (!isUnderground && distance > radius)
                            continue;
                        if (isUnderground && !isNearZero)
                            continue;

                        string lowerName = entityName.ToLower();
                        bool shouldDelete = false;

                        // Проверяем ключевые слова канистр
                        foreach (var keyword in canisterKeywords)
                        {
                            if (lowerName.Contains(keyword))
                            {
                                shouldDelete = true;
                                break;
                            }
                        }

                        // Расширенный список ключевых слов и префабов для светильников и источников света
                        string[] lightKeywords = new string[] {
                            // Префабы и ключевые слова для всех типов светильников и источников света
                            "ceilinglight", "ceilinglight.deployed", "simplelight", "simplelight.deployed", "searchlight", "searchlight.deployed",
                            "lantern", "lantern.deployed", "candle", "candle.deployed", "campfire", "campfire.deployed", "fireplace", "fireplace.deployed",
                            "fogmachine", "fogmachine.deployed", "disco", "disco.deployed", "strobe", "strobe.deployed", "laser", "laser.deployed",
                            "neon", "neon.deployed", "lamp", "lamp.deployed", "light", "light source", "sirenlight", "flasherlight", "industrial.wall.lamp",
                            "jackolantern", "skullspikes.candles", "skull_fire_pit", "cursedcauldron", "electricfurnace", "oven", "baseoven", "basefuellightsource",
                            // Общие ключевые слова
                            "light", "glow", "flare", "spark", "fire", "smoke", "particle", "vfx", "fx", "decal"
                        };
                        foreach (var keyword in lightKeywords)
                        {
                            if (lowerName.Contains(keyword))
                            {
                                shouldDelete = true;
                                break;
                            }
                        }

                        // Дополнительная проверка компонентов для канистр
                        if (!shouldDelete)
                        {
                            try
                            {
                                if (entity.GetComponent<StorageContainer>() != null || 
                                    entity.GetComponent<LiquidContainer>() != null ||
                                    entity.GetComponent<FuelGenerator>() != null ||
                                    entity.GetComponent<BaseFuelLightSource>() != null)
                                {
                                    shouldDelete = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                // Игнорируем ошибки доступа к компонентам
                                LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Error checking canister components for {entityName}: {ex.Message}", this);
                            }
                        }

                        // Проверка защищенной зоны для канистр
                        if (IsInProtectedZone(entityPos))
                        {
                            shouldDelete = true;
                            LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Canister in protected zone detected: {entityName} at ({entityPos.x:F1}, {entityPos.y:F1}, {entityPos.z:F1})", this);
                        }

                        if (shouldDelete)
                        {
                            try
                            {
                                entity.Kill();
                                removedCount++;
                                batchSize++;
                                processedCount++;
                                
                                string positionInfo = isUnderground ? $"underground at ({entityPos.x:F1}, {entityPos.y:F1}, {entityPos.z:F1})" : $"at distance {distance:F1}m";
                                string removalMessage = $"Deleted canister {entityName} {positionInfo} from center position ({config.PositionDeletions.x}, {config.PositionDeletions.y}, {config.PositionDeletions.z})";
                                
                                if (removedCount % 50 == 0)
                                {
                                    PrintWarning($"Deleted {removedCount} canisters so far...");
                                }
                                LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {removalMessage}", this);
                                
                                if (batchSize >= maxBatchSize)
                                {
                                    batchSize = 0;
                                    // Используем NextTick вместо Thread.Sleep для неблокирующей паузы
                                    NextTick(() => { });
                                }
                            }
                            catch (Exception ex)
                            {
                                LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Error deleting canister {entityName}: {ex.Message}", this);
                            }
                        }
                        else
                        {
                            processedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Error processing canister entity: {ex.Message}", this);
                        continue;
                    }
                }

                if (removedCount > 0)
                {
                    string logMessage = $"Deleted {removedCount} canisters within {config.DistanceRadius}m of position {config.PositionDeletions.x}, {config.PositionDeletions.y}, {config.PositionDeletions.z}. Processed {processedCount} total entities.";
                    PrintWarning(logMessage);
                    LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {logMessage}", this);
                }
                else
                {
                    LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: No canisters deleted. Processed {processedCount} total entities.", this);
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Critical error in RemoveCanistersOnly: {ex.Message}";
                PrintError(errorMessage);
                LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {errorMessage}\nStackTrace: {ex.StackTrace}", this);
            }
        }

        private void ScanAllObjects()
        {
            try
            {
                Vector3 center = new Vector3(config.PositionDeletions.x, config.PositionDeletions.y, config.PositionDeletions.z);
                float radius = config.DistanceRadius;
                LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Starting area scan. Center: ({center.x}, {center.y}, {center.z}), Radius: {radius}m", this);

                var entities = SafeFindEntities(center, radius, "Area scan");

                int inRadiusCount = 0;
                int undergroundCount = 0;
                int canisterCount = 0;
                int codeLockCount = 0;
                int otherCount = 0;

                foreach (var entity in entities)
                {
                    try
                    {
                        if (entity == null || entity.IsDestroyed)
                            continue;
                        if (entity is BasePlayer)
                            continue;

                        string entityName = entity.ShortPrefabName;
                        Vector3 entityPos = entity.transform.position;
                        float distance = Vector3.Distance(center, entityPos);
                        bool isUnderground = entityPos.y < -100f;
                        bool isNearZero = Math.Abs(entityPos.x) < radius && Math.Abs(entityPos.z) < radius;

                        // Проверяем, находится ли объект в радиусе
                        bool inRadius = distance <= radius || (isUnderground && isNearZero);

                        if (inRadius)
                        {
                            inRadiusCount++;
                            string positionInfo = isUnderground ? $"underground at ({entityPos.x:F1}, {entityPos.y:F1}, {entityPos.z:F1})" : $"at distance {distance:F1}m";
                            
                            // Категоризируем объекты
                            string lowerName = entityName.ToLower();
                            bool isCanister = lowerName.Contains("jerrycan") || lowerName.Contains("gascan") || lowerName.Contains("fuelcan") || 
                                            lowerName.Contains("fuel") || lowerName.Contains("gas") || lowerName.Contains("oil") ||
                                            lowerName.Contains("water") || lowerName.Contains("can") || lowerName.Contains("jug") ||
                                            lowerName.Contains("bottle") || lowerName.Contains("container") || lowerName.Contains("barrel") ||
                                            lowerName.Contains("storage") || lowerName.Contains("tank") || lowerName.Contains("drum") ||
                                            lowerName.Contains("cask") || lowerName.Contains("vessel") || lowerName.Contains("reservoir") ||
                                            lowerName.Contains("cistern") || lowerName.Contains("tank") || lowerName.Contains("canister") ||
                                            lowerName.Contains("fuel_storage") || lowerName.Contains("water_storage") || lowerName.Contains("oil_storage");
                            
                            bool isCodeLock = lowerName.Contains("codelock") || lowerName.Contains("code.lock") || 
                                            lowerName.Contains("lock.code") || lowerName.Contains("lock.codelock") ||
                                            lowerName.Contains("code") || lowerName.Contains("lock") || lowerName.Contains("keypad") ||
                                            lowerName.Contains("key") || lowerName.Contains("pad") || lowerName.Contains("combination") ||
                                            lowerName.Contains("pin") || lowerName.Contains("password") || lowerName.Contains("security");

                            if (isUnderground) undergroundCount++;
                            if (isCanister) canisterCount++;
                            if (isCodeLock) codeLockCount++;
                            if (!isCanister && !isCodeLock) otherCount++;

                            LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Found object: {entityName} {positionInfo} - Type: {(isCanister ? "CANISTER" : isCodeLock ? "CODELOCK" : "OTHER")}", this);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Error scanning entity: {ex.Message}", this);
                    }
                }

                LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Scan summary - In radius: {inRadiusCount}, Underground: {undergroundCount}, Canisters: {canisterCount}, Code locks: {codeLockCount}, Other: {otherCount}", this);
                PrintWarning($"Area scan completed: {inRadiusCount} objects in radius, {canisterCount} canisters, {codeLockCount} code locks");
            }
            catch (Exception ex)
            {
                string errorMessage = $"Critical error in ScanAllObjects: {ex.Message}";
                PrintError(errorMessage);
                LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {errorMessage}\nStackTrace: {ex.StackTrace}", this);
            }
        }

        private void ForceDeleteAll()
        {
            try
            {
                Vector3 center = new Vector3(config.PositionDeletions.x, config.PositionDeletions.y, config.PositionDeletions.z);
                float radius = config.DistanceRadius;
                LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Starting FORCE deletion. Center: ({center.x}, {center.y}, {center.z}), Radius: {radius}m", this);

                var entities = SafeFindEntities(center, radius, "Force deletion");

                int deletedCount = 0;
                int skippedCount = 0;

                foreach (var entity in entities)
                {
                    try
                    {
                        if (entity == null || entity.IsDestroyed)
                            continue;
                        if (entity is BasePlayer)
                        {
                            skippedCount++;
                            continue;
                        }

                        string entityName = entity.ShortPrefabName;
                        Vector3 entityPos = entity.transform.position;
                        float distance = Vector3.Distance(center, entityPos);
                        bool isUnderground = entityPos.y < -100f;
                        bool isNearZero = Math.Abs(entityPos.x) < radius && Math.Abs(entityPos.z) < radius;

                        // Проверяем, находится ли объект в радиусе
                        bool inRadius = distance <= radius || (isUnderground && isNearZero);

                        if (inRadius)
                        {
                            try
                            {
                                // Принудительное удаление - игнорируем все исключения
                                entity.Kill();
                                deletedCount++;
                                
                                string positionInfo = isUnderground ? $"underground at ({entityPos.x:F1}, {entityPos.y:F1}, {entityPos.z:F1})" : $"at distance {distance:F1}m";
                                string removalMessage = $"FORCE DELETED: {entityName} {positionInfo} from center position ({config.PositionDeletions.x}, {config.PositionDeletions.y}, {config.PositionDeletions.z})";
                                
                                PrintWarning(removalMessage);
                                LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {removalMessage}", this);
                            }
                            catch (Exception ex)
                            {
                                LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Failed to force delete {entityName}: {ex.Message}", this);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Error processing entity for force deletion: {ex.Message}", this);
                    }
                }

                LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Force deletion completed - Deleted: {deletedCount}, Skipped: {skippedCount}", this);
                PrintWarning($"FORCE DELETION COMPLETED: {deletedCount} objects deleted, {skippedCount} skipped");
            }
            catch (Exception ex)
            {
                string errorMessage = $"Critical error in ForceDeleteAll: {ex.Message}";
                PrintError(errorMessage);
                LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {errorMessage}\nStackTrace: {ex.StackTrace}", this);
            }
        }

        private void NukeProblematicArea()
        {
            try
            {
                Vector3 center = new Vector3(config.PositionDeletions.x, config.PositionDeletions.y, config.PositionDeletions.z);
                float radius = config.DistanceRadius;
                LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Starting NUKE operation. Center: ({center.x}, {center.y}, {center.z}), Radius: {radius}m", this);

                var entities = SafeFindEntities(center, radius, "NUKE operation");

                if (entities.Count > 10000)
                {
                    LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: WARNING: Massive object spam detected! {entities.Count} objects found!", this);
                    PrintWarning($"MASSIVE OBJECT SPAM DETECTED: {entities.Count} objects! This will cause severe lag!");
                }

                int deletedCount = 0;
                int skippedCount = 0;
                int batchSize = 0;
                int maxBatchSize = 100; // Максимум 100 объектов за раз

                foreach (var entity in entities)
                {
                    try
                    {
                        if (entity == null || entity.IsDestroyed)
                            continue;
                        if (entity is BasePlayer)
                        {
                            skippedCount++;
                            continue;
                        }

                        string entityName = entity.ShortPrefabName;
                        Vector3 entityPos = entity.transform.position;
                        float distance = Vector3.Distance(center, entityPos);
                        bool isUnderground = entityPos.y < -100f;
                        bool isNearZero = Math.Abs(entityPos.x) < radius && Math.Abs(entityPos.z) < radius;

                        // Проверяем, находится ли объект в радиусе
                        bool inRadius = distance <= radius || (isUnderground && isNearZero);

                        if (inRadius)
                        {
                            try
                            {
                                // Принудительное удаление с дополнительными проверками
                                entity.Kill();
                                deletedCount++;
                                batchSize++;

                                // Логируем только каждые 100 удалений для экономии места
                                if (deletedCount % 100 == 0)
                                {
                                    string positionInfo = isUnderground ? $"underground at ({entityPos.x:F1}, {entityPos.y:F1}, {entityPos.z:F1})" : $"at distance {distance:F1}m";
                                    LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: NUKE deleted {deletedCount} objects so far. Current: {entityName} {positionInfo}", this);
                                }

                                // Ограничиваем размер пакета для предотвращения лагов
                                if (batchSize >= maxBatchSize)
                                {
                                    LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Batch limit reached ({maxBatchSize}). Pausing for performance.", this);
                                    batchSize = 0;
                                    // Небольшая пауза для стабилизации производительности
                                    // Используем NextTick вместо Thread.Sleep для неблокирующей паузы
                                    NextTick(() => { });
                                }
                            }
                            catch (Exception ex)
                            {
                                LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Failed to NUKE delete {entityName}: {ex.Message}", this);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Error processing entity for NUKE: {ex.Message}", this);
                    }
                }

                LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: NUKE operation completed - Deleted: {deletedCount}, Skipped: {skippedCount}", this);
                PrintWarning($"NUKE OPERATION COMPLETED: {deletedCount} objects deleted, {skippedCount} skipped");
                
                if (deletedCount > 1000)
                {
                    PrintWarning($"MASSIVE CLEANUP: Removed {deletedCount} spam objects! Server performance should improve significantly.");
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Critical error in NukeProblematicArea: {ex.Message}";
                PrintError(errorMessage);
                LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {errorMessage}\nStackTrace: {ex.StackTrace}", this);
            }
        }

        private void CleanProtectedZone()
        {
            try
            {
                Vector3 zeroPoint = Vector3.zero;
                float zoneRadius = 50f; // Радиус защищенной зоны
                LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Starting protected zone cleanup. Center: ({zeroPoint.x}, {zeroPoint.y}, {zeroPoint.z}), ZoneRadius: {zoneRadius}m", this);

                var entities = SafeFindEntities(zeroPoint, zoneRadius, "Protected zone cleanup");

                int deletedCount = 0;
                int skippedCount = 0;

                foreach (var entity in entities)
                {
                    try
                    {
                        if (entity == null || entity.IsDestroyed)
                            continue;
                        if (entity is BasePlayer)
                        {
                            skippedCount++;
                            continue;
                        }

                        string entityName = entity.ShortPrefabName;
                        Vector3 entityPos = entity.transform.position;
                        float distance = Vector3.Distance(zeroPoint, entityPos);
                        bool isUnderground = entityPos.y < -100f;
                        bool isNearZero = Math.Abs(entityPos.x) < zoneRadius && Math.Abs(entityPos.z) < zoneRadius;

                        // Проверяем, находится ли объект в защищенной зоне
                        bool inProtectedZone = distance <= zoneRadius || (isUnderground && isNearZero);

                        if (inProtectedZone)
                        {
                            try
                            {
                                // Удаляем объект в защищенной зоне
                                entity.Kill();
                                deletedCount++;
                                
                                string positionInfo = isUnderground ? $"underground at ({entityPos.x:F1}, {entityPos.y:F1}, {entityPos.z:F1})" : $"at distance {distance:F1}m";
                                string removalMessage = $"PROTECTED ZONE DELETED: {entityName} {positionInfo} from zero point";
                                
                                PrintWarning(removalMessage);
                                LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {removalMessage}", this);
                            }
                            catch (Exception ex)
                            {
                                LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Failed to delete {entityName} in protected zone: {ex.Message}", this);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Error processing entity for protected zone cleanup: {ex.Message}", this);
                    }
                }

                LogToFile("removeTrash.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: Protected zone cleanup completed - Deleted: {deletedCount}, Skipped: {skippedCount}", this);
                PrintWarning($"PROTECTED ZONE CLEANUP COMPLETED: {deletedCount} objects deleted, {skippedCount} skipped");
                
                if (deletedCount > 0)
                {
                    PrintWarning($"Zero point protection: Removed {deletedCount} objects from protected zone!");
                }
            }
            catch (Exception ex)
            {
                string errorMessage = $"Critical error in CleanProtectedZone: {ex.Message}";
                PrintError(errorMessage);
                LogToFile("removeTrash_errors.log", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]: {errorMessage}\nStackTrace: {ex.StackTrace}", this);
            }
        }

        void Unload()
        {
            // Очищаем таймер при выгрузке плагина
            if (cleanupTimer != null)
                cleanupTimer.Destroy();
        }
    }
}
