// ДАННЫЙ ПЛАГИН МОЖНО БЕСПЛАТНО СКАЧАТЬ НА САЙТЕ @https://oxide-russia.ru/
// Автор: AKUSIK
// Версия: 0.0.1
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Net;
using System.Text;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Cleaner", "AKUSIK", "0.0.1")]
    [Description("Очистка сервера от мусора")]
    public class Cleaner : RustPlugin
    {
        private ConfigData config;
        class ConfigData
        {
            [JsonProperty("Префикс сообщения")] public string MessagePrefix = "[Clear]";
            [JsonProperty("Интервал очистки (в секундах)")] public double CleanInterval = 200.0;
            [JsonProperty("Очищать выброшенные предметы")] public bool CleanDroppedItems = true;
            [JsonProperty("Очищать трупы игроков")] public bool CleanPlayerCorpses = true;
            [JsonProperty("Очищать рюкзаки NPC")] public bool CleanNPCCorpses = true;
            [JsonProperty("Очищать рюкзаки из предметов")] public bool CleanBackpacksFromItems = true;
            [JsonProperty("Очищать трупы с лутом")] public bool CleanLootedCorpses = true;
            [JsonProperty("Очищать неактивные сущности")] public bool CleanInactiveEntities = true;
            [JsonProperty("Время уведомления перед очисткой (сек)")] public double NotifyBeforeClean = 30.0;
            [JsonProperty("Список исключённых предметов")] public List<string> ExcludedItems = new List<string>();
            [JsonProperty("Включить уведомления об очистке (в чат)")] public bool EnableChatNotify = true;
            [JsonProperty("Отключить коллизии у предметов")] public bool DisableItemColliders = false;
            [JsonProperty("Включить авто-очистку по типам предметов")] public bool EnableTypeAutoClean = false;
            [JsonProperty("Порог авто-очистки по типам предметов")] public int TypeAutoCleanThreshold = 100;
            [JsonProperty("Включить пошаговую очистку")] public bool EnableStepClean = true;
            [JsonProperty("Размер пакета очистки")] public int StepCleanBatchSize = 50;
            [JsonProperty("Задержка между пакетами очистки (сек)")] public double StepCleanDelay = 0.5;
            [JsonProperty("Кастомная команда для бана")] public string CustomBanCommand = "banid";
            [JsonProperty("Кастомные предметы для очистки на нулевых координатах")] public List<string> CustomZeroCoordItems = new List<string>();
            [JsonProperty("DiscordWebhookUrl")] public string DiscordWebhookUrl = "";
        }
        protected override void LoadDefaultConfig() => config = new ConfigData();
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>() ?? new ConfigData();
                SaveConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }
        }
        protected override void SaveConfig() => Config.WriteObject(config);

        private int lastCleanedCount = 0;
        private Timer cleanTimer;

        void Init()
        {
            LoadConfig();
            cleanTimer = timer.Every((float)config.CleanInterval, () => RunAutoClean());
            AddCovalenceCommand("clear.force", "CmdForceClean");
        }

        void Unload()
        {
            cleanTimer?.Destroy();
        }

        void RunAutoClean()
        {
            if (config.NotifyBeforeClean > 0)
            {
                timer.Once((float)config.NotifyBeforeClean, () =>
                {
                    if (config.EnableChatNotify)
                        PrintToChat($"{config.MessagePrefix} Очистка через {config.NotifyBeforeClean} сек.");
                });
            }
            timer.Once((float)config.NotifyBeforeClean, () =>
            {
                var report = new CleanReport();
                if (config.CleanDroppedItems) report.DroppedItems = CleanDroppedItems();
                if (config.CleanPlayerCorpses) report.PlayerCorpses = CleanPlayerCorpses();
                if (config.CleanNPCCorpses) report.NPCCorpses = CleanNPCCorpses();
                if (config.CleanBackpacksFromItems) report.Backpacks = CleanBackpacksFromItems();
                if (config.CleanLootedCorpses) report.LootedCorpses = CleanLootedCorpses();
                if (config.CleanInactiveEntities) report.InactiveEntities = CleanInactiveEntities();
                report.ZeroCoordObjects = CleanZeroCoordObjects();
                lastCleanedCount = report.Total;
                SendCleanReport(report);
            });
        }

        class CleanReport
        {
            public int DroppedItems;
            public int PlayerCorpses;
            public int NPCCorpses;
            public int Backpacks;
            public int LootedCorpses;
            public int InactiveEntities;
            public int ZeroCoordObjects;
            public int Total => DroppedItems + PlayerCorpses + NPCCorpses + Backpacks + LootedCorpses + InactiveEntities + ZeroCoordObjects;
        }

        int CleanAll()
        {
            var report = new CleanReport();
            if (config.CleanDroppedItems) report.DroppedItems = CleanDroppedItems();
            if (config.CleanPlayerCorpses) report.PlayerCorpses = CleanPlayerCorpses();
            if (config.CleanNPCCorpses) report.NPCCorpses = CleanNPCCorpses();
            if (config.CleanBackpacksFromItems) report.Backpacks = CleanBackpacksFromItems();
            if (config.CleanLootedCorpses) report.LootedCorpses = CleanLootedCorpses();
            if (config.CleanInactiveEntities) report.InactiveEntities = CleanInactiveEntities();
            report.ZeroCoordObjects = CleanZeroCoordObjects();
            lastCleanedCount = report.Total;
            SendCleanReport(report);
            return report.Total;
        }

        int CleanDroppedItems()
        {
            int count = 0;
            var toRemove = new List<BaseEntity>();
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is DroppedItem item)
                {
                    if (item == null || item.IsDestroyed) continue;
                    if (item.item == null || IsExcluded(item.item.info.shortname)) continue;
                    toRemove.Add(item);
                }
            }
            foreach (var entity in toRemove)
            {
                entity.Kill();
                count++;
            }
            return count;
        }

        int CleanPlayerCorpses()
        {
            int count = 0;
            var toRemove = new List<BaseEntity>();
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is PlayerCorpse corpse && corpse.playerName != null)
                {
                    if (corpse.IsDestroyed) continue;
                    toRemove.Add(corpse);
                }
            }
            foreach (var entity in toRemove)
            {
                entity.Kill();
                count++;
            }
            return count;
        }

        int CleanNPCCorpses()
        {
            int count = 0;
            var toRemove = new List<BaseEntity>();
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is NPCPlayerCorpse corpse && corpse.IsNpc)
                {
                    if (corpse.IsDestroyed) continue;
                    toRemove.Add(corpse);
                }
            }
            foreach (var entity in toRemove)
            {
                entity.Kill();
                count++;
            }
            return count;
        }

        int CleanBackpacksFromItems()
        {
            int count = 0;
            var toRemove = new List<BaseEntity>();
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is BaseEntity be && be.ShortPrefabName.Contains("backpack"))
                {
                    if (be.IsDestroyed) continue;
                    toRemove.Add(be);
                }
            }
            foreach (var entity in toRemove)
            {
                entity.Kill();
                count++;
            }
            return count;
        }

        int CleanLootedCorpses()
        {
            int count = 0;
            var toRemove = new List<BaseEntity>();
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is DroppedItemContainer container && container.inventory != null && container.inventory.itemList.Count > 0)
                {
                    if (container.IsDestroyed) continue;
                    toRemove.Add(container);
                }
            }
            foreach (var entity in toRemove)
            {
                entity.Kill();
                count++;
            }
            return count;
        }

        int CleanInactiveEntities()
        {
            int count = 0;
            var toRemove = new List<BaseEntity>();
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is BaseEntity be)
                {
                    if (be == null || be.IsDestroyed) continue;
                }
            }
            return count;
        }

        int CleanZeroCoordObjects()
        {
            int count = 0;
            var toRemove = new List<BaseEntity>();
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is BaseEntity be)
                {
                    if (be == null || be.IsDestroyed) continue;
                    var pos = be.transform.position;
                    if (Mathf.Approximately(pos.x, 0f) && Mathf.Approximately(pos.y, 0f) && Mathf.Approximately(pos.z, 0f))
                    {
                        if (config.CustomZeroCoordItems.Any(type => be.ShortPrefabName.Contains(type)))
                        {
                            toRemove.Add(be);
                        }
                    }
                }
            }
            foreach (var entity in toRemove)
            {
                entity.Kill();
                count++;
            }
            return count;
        }

        void StepClean()
        {
            var toRemove = new List<BaseEntity>();
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is DroppedItem item && !IsExcluded(item.item?.info?.shortname ?? ""))
                {
                    toRemove.Add(item);
                }
            }
            int total = toRemove.Count;
            int batchSize = config.StepCleanBatchSize;
            float delay = (float)config.StepCleanDelay;
            for (int i = 0; i < total; i += batchSize)
            {
                var batch = toRemove.Skip(i).Take(batchSize).ToList();
                timer.Once(delay * (i / batchSize), () =>
                {
                    foreach (var entity in batch)
                    {
                        entity.Kill();
                    }
                });
            }
        }

        void CheckTypeAutoClean()
        {
            if (!config.EnableTypeAutoClean) return;
            var typeCounts = new Dictionary<string, int>();
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is DroppedItem item && item.item != null)
                {
                    var shortname = item.item.info.shortname;
                    if (!typeCounts.ContainsKey(shortname)) typeCounts[shortname] = 0;
                    typeCounts[shortname]++;
                }
            }
            foreach (var kv in typeCounts)
            {
                if (kv.Value >= config.TypeAutoCleanThreshold)
                {
                    foreach (var entity in BaseNetworkable.serverEntities)
                    {
                        if (entity is DroppedItem item && item.item != null && item.item.info.shortname == kv.Key)
                        {
                            item.Kill();
                        }
                    }
                    if (config.EnableChatNotify)
                        PrintToChat($"{config.MessagePrefix} Авто-очистка предметов типа {kv.Key}: {kv.Value} шт.");
                }
            }
        }

        void DisableColliders() { }

        bool IsExcluded(string shortname) => config.ExcludedItems.Contains(shortname);

        [Command("clear.force")]
        void CmdForceClean(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.Reply("Нет прав.");
                return;
            }
            var report = new CleanReport();
            if (config.CleanDroppedItems) report.DroppedItems = CleanDroppedItems();
            if (config.CleanPlayerCorpses) report.PlayerCorpses = CleanPlayerCorpses();
            if (config.CleanNPCCorpses) report.NPCCorpses = CleanNPCCorpses();
            if (config.CleanBackpacksFromItems) report.Backpacks = CleanBackpacksFromItems();
            if (config.CleanLootedCorpses) report.LootedCorpses = CleanLootedCorpses();
            if (config.CleanInactiveEntities) report.InactiveEntities = CleanInactiveEntities();
            report.ZeroCoordObjects = CleanZeroCoordObjects();
            lastCleanedCount = report.Total;
            SendCleanReport(report);
            player.Reply($"{config.MessagePrefix} Принудительно очищено объектов: {report.Total}");
        }

        void SendCleanReport(CleanReport report)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"{config.MessagePrefix} Очистка завершена. Всего удалено: {report.Total}");
            if (report.DroppedItems > 0) sb.AppendLine($"- Выброшенные предметы: {report.DroppedItems}");
            if (report.PlayerCorpses > 0) sb.AppendLine($"- Трупы игроков: {report.PlayerCorpses}");
            if (report.NPCCorpses > 0) sb.AppendLine($"- Трупы NPC: {report.NPCCorpses}");
            if (report.Backpacks > 0) sb.AppendLine($"- Рюкзаки: {report.Backpacks}");
            if (report.LootedCorpses > 0) sb.AppendLine($"- Лутовые контейнеры: {report.LootedCorpses}");
            if (report.InactiveEntities > 0) sb.AppendLine($"- Неактивные сущности: {report.InactiveEntities}");
            if (report.ZeroCoordObjects > 0) sb.AppendLine($"- Объекты на (0,0,0): {report.ZeroCoordObjects}");
            string msg = sb.ToString().Trim();
            if (config.EnableChatNotify)
                PrintToChat(msg);
            LogToDiscord(msg);
        }

        void LogToDiscord(string message)
        {
            if (string.IsNullOrEmpty(config.DiscordWebhookUrl)) return;
            SendDiscordWebhook(message);
        }

        void SendDiscordWebhook(string message)
        {
            var payload = new { content = message };
            string json = JsonConvert.SerializeObject(payload);
            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                client.Encoding = System.Text.Encoding.UTF8;
                try { client.UploadString(config.DiscordWebhookUrl, "POST", json); } catch { }
            }
        }
    }
} 