using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Auto Cleaner", "Termin", "1.2")]
    class AutoCleaner : RustPlugin
    {
        private Timer cleanupTimer;
        private const string LootBagPrefab = "assets/prefabs/misc/item drop/item_drop_backpack.prefab";
        private const string ItemDropPrefab = "assets/prefabs/misc/item drop/item_drop.prefab";
        private const float CLEANUP_INTERVAL = 0.5f;
        private const int ITEMS_PER_BATCH = 10;
        private const float AUTO_CLEANUP_TIME = 2400f; // 30 دقیقه

        private int totalItems = 0;
        private int totalCorpses = 0;
        private int totalBags = 0;

        void Init()
        {
            cleanupTimer = timer.Every(AUTO_CLEANUP_TIME, () => StartWarnings());
            Puts("Auto Cleaner initialized - Cleanup every 30 minutes");
        }

        void Unload()
        {
            cleanupTimer?.Destroy();
        }

        void StartWarnings()
        {
            // 60 second warning
            BroadcastWarning(60);

            // 30 second warning
            timer.Once(30f, () => BroadcastWarning(30));

            // 10 second warning
            timer.Once(50f, () => BroadcastWarning(10));

            // Start cleanup after 60 seconds
            timer.Once(60f, () => StartCleanup());
        }

        void BroadcastWarning(int seconds)
        {
            Server.Broadcast($"<color=#FFE400>[PersianToxic]</color> <color=#FF0000>WARNING:</color> <color=#98FB98>Ground items will be cleaned in</color> <color=#FF0000>{seconds} seconds!</color>");
        }

        void StartCleanup()
        {
            Server.Broadcast("<color=#FFE400>[PersianToxic]</color> <color=#00FF00>Starting</color> <color=#98FB98>cleanup process</color>");

            totalItems = 0;
            totalCorpses = 0;
            totalBags = 0;

            timer.Once(2f, () => StartGradualCleanup());
        }

        void StartGradualCleanup()
        {
            // اول کیف‌ها و item_drop ها را پاک می‌کنیم
            var bags = UnityEngine.Object.FindObjectsOfType<DroppedItemContainer>()
                .Where(bag => bag != null && !bag.IsDestroyed && 
                    (bag.PrefabName == LootBagPrefab || bag.PrefabName == ItemDropPrefab))
                .ToList();

            CleanupBagsGradually(bags, () => {
                // بعد جنازه‌ها را پاک می‌کنیم
                var corpses = UnityEngine.Object.FindObjectsOfType<BaseCorpse>()
                    .Where(corpse => corpse != null && !corpse.IsDestroyed)
                    .ToList();

                CleanupCorpsesGradually(corpses, () => {
                    // در نهایت آیتم‌های روی زمین را پاک می‌کنیم
                    var items = UnityEngine.Object.FindObjectsOfType<DroppedItem>()
                        .Where(item => item != null && !item.IsDestroyed)
                        .ToList();

                    CleanupItemsGradually(items, ShowFinalReport);
                });
            });
        }

        void CleanupBagsGradually(List<DroppedItemContainer> bags, System.Action onComplete)
        {
            if (bags.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            var batch = bags.Take(ITEMS_PER_BATCH).ToList();
            bags.RemoveRange(0, Mathf.Min(ITEMS_PER_BATCH, bags.Count));

            foreach (var bag in batch)
            {
                if (bag != null && !bag.IsDestroyed)
                {
                    bag.Kill();
                    totalBags++;
                }
            }

            if (bags.Count > 0)
                timer.Once(CLEANUP_INTERVAL, () => CleanupBagsGradually(bags, onComplete));
            else
                onComplete?.Invoke();
        }

        void CleanupCorpsesGradually(List<BaseCorpse> corpses, System.Action onComplete)
        {
            if (corpses.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            var batch = corpses.Take(ITEMS_PER_BATCH).ToList();
            corpses.RemoveRange(0, Mathf.Min(ITEMS_PER_BATCH, corpses.Count));

            foreach (var corpse in batch)
            {
                if (corpse != null && !corpse.IsDestroyed)
                {
                    corpse.Kill();
                    totalCorpses++;
                }
            }

            if (corpses.Count > 0)
                timer.Once(CLEANUP_INTERVAL, () => CleanupCorpsesGradually(corpses, onComplete));
            else
                onComplete?.Invoke();
        }

        void CleanupItemsGradually(List<DroppedItem> items, System.Action onComplete)
        {
            if (items.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            var batch = items.Take(ITEMS_PER_BATCH).ToList();
            items.RemoveRange(0, Mathf.Min(ITEMS_PER_BATCH, items.Count));

            foreach (var item in batch)
            {
                if (item != null && !item.IsDestroyed)
                {
                    item.Kill();
                    totalItems++;
                }
            }

            if (items.Count > 0)
                timer.Once(CLEANUP_INTERVAL, () => CleanupItemsGradually(items, onComplete));
            else
                onComplete?.Invoke();
        }

        void ShowFinalReport()
        {
            string report = $@"<color=#FFE400>[PersianToxic]</color> <color=#00FF00>Cleanup Complete!</color>
<color=#00FF00>• <color=#98FB98>Items</color> Removed: {totalItems}</color>
<color=#00FF00>• <color=#98FB98>Corpses</color> Removed: {totalCorpses}</color>
<color=#00FF00>• <color=#98FB98>Bags</color> Removed: {totalBags}</color>
<color=#98FB98>Total items cleaned: {totalItems + totalCorpses + totalBags}</color>";

            Server.Broadcast(report);
            Puts($"Cleanup complete - Items: {totalItems}, Corpses: {totalCorpses}, Bags: {totalBags}");
        }

        [ChatCommand("cleanup")]
        void CleanupCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage("<color=#FF0000>You don't have permission to use this command!</color>");
                return;
            }
            StartWarnings();
            player.ChatMessage("<color=#98FB98>Cleanup process initiated!</color>");
        }

        [ConsoleCommand("cleanup.now")]
        void ConsoleCleanup(ConsoleSystem.Arg arg)
        {
            if (arg.IsAdmin)
            {
                StartWarnings();
                Puts("Cleanup process initiated by console command");
            }
        }
    }
}