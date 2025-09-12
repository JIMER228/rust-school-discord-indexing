using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.IO;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("AutoClean", "RustRearm", "1.5.7")]
    [Description("Automatically cleans the map of dropped items to maintain server performance, excluding 'item_drop_backpack' and 'item_drop' by default.")]
    public class AutoClean : RustPlugin
    {
        private const string PermUse = "autoclean.use";
        private const string PermAdmin = "autoclean.admin";

        private PluginConfig config;
        private Timer countdownTimer;
        private float timeRemaining;
        private readonly Dictionary<ulong, List<BaseEntity>> playerDroppedItems = new Dictionary<ulong, List<BaseEntity>>();

        [PluginReference]
        private Plugin ZoneManager;

        #region Configuration

        private class PluginConfig
        {
            public float CleanIntervalMinutes = 30f;
            public string Prefix = "<color=white>[<color=red>AutoClean</color>]</color>";
            public string TimerColor = "<color=green>";

            
            public List<int> ReminderMinutes = new List<int> { 5, 4, 3, 2, 1 };

            
            public List<string> ExcludedItemShortNames = new List<string>
            {
                "largebackpack",
                "smallbackackpack",
                "keycard_blue",
                "keycard_green",
                "keycard_red"
            };

           
            public List<string> ExcludedZoneIDs = new List<string>
            {
              
            };

        
            public bool EnableLogging = true;
            public bool EnablePlayerNotifications = true;
            public bool EnableAdminNotifications = false;

            
            public List<string> CleanItemShortNames = new List<string>
            {
              
            };
        }

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            if (config == null)
            {
                PrintWarning("Configuration file is corrupt. Loading default configuration.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);

        #endregion

        #region Initialization & Cleanup

        private void OnServerInitialized()
        {
            permission.RegisterPermission(PermUse, this);
            permission.RegisterPermission(PermAdmin, this);

            timeRemaining = config.CleanIntervalMinutes * 60f;
            StartCountdown();

            if (config.EnablePlayerNotifications || config.EnableAdminNotifications)
            {
                Subscribe(nameof(OnEntityDropped));
            }

            EnsureDirectoriesExist();
            LoadDefaultMessages();
        }

        private void Unload()
        {
            countdownTimer?.Destroy();
        }

        private void StartCountdown()
        {
            countdownTimer = timer.Every(1f, () =>
            {
                timeRemaining -= 1f;
                var minutesRemaining = Mathf.CeilToInt(timeRemaining / 60f);

                if (config.ReminderMinutes.Contains(minutesRemaining) && Mathf.Approximately(timeRemaining % 60f, 0f))
                {
                    SendLocalizedNotificationToAll("MAP_WILL_BE_CLEARED", minutesRemaining);
                    NotifyPlayers();
                }


                if (Mathf.Approximately(timeRemaining, 0f))
                {
                    CleanMap();
                    timeRemaining = config.CleanIntervalMinutes * 60f;
                }
            });
        }

        private void CleanMap()
        {
            int itemsRemoved = 0;
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logEntries = new List<string>();


            foreach (var netEntity in BaseNetworkable.serverEntities)
            {
                var entity = netEntity as BaseEntity;
                if (entity == null) continue;

                if (IsParachute(entity))
                {
                    if (config.EnableLogging)
                    {
                        logEntries.Add($"[{now}] Removed parachute entity (Prefab: {entity.ShortPrefabName})");
                    }
                    KillEntity(entity);
                    itemsRemoved++;
                    continue;
                }


                if (!(entity is DroppedItem) && !(entity is DroppedItemContainer)) continue;
                if (IsInExcludedZone(entity)) continue;

                var droppedItem = entity as DroppedItem;
                var droppedContainer = entity as DroppedItemContainer;


                if (droppedItem != null)
                {

                    if ((entity.ShortPrefabName == "item_drop" || entity.ShortPrefabName == "item_drop_backpack") &&
                        !config.CleanItemShortNames.Contains(entity.ShortPrefabName))
                    {
                        continue;
                    }

                    var shortName = droppedItem.item?.info?.shortname ?? string.Empty;
                    if (ShouldRemove(shortName))
                    {
                        if (config.EnableLogging)
                        {
                            var itemName = droppedItem.item?.info?.displayName?.english ?? "Unknown";
                            var ownerID = droppedItem.OwnerID.ToString();
                            logEntries.Add($"[{now}] Removed Item: {itemName} ({shortName}), OwnerID: {ownerID}");
                        }
                        KillEntity(entity);
                        itemsRemoved++;
                    }
                }

                else if (droppedContainer != null)
                {

                    if ((entity.ShortPrefabName == "item_drop" || entity.ShortPrefabName == "item_drop_backpack") &&
                        !config.CleanItemShortNames.Contains(entity.ShortPrefabName))
                    {
                        continue;
                    }

                    bool hasExcluded = false;
                    bool hasRemovable = false;
                    foreach (var item in droppedContainer.inventory.itemList)
                    {
                        if (IsExcludedItem(item.info.shortname))
                        {
                            hasExcluded = true;
                            break;
                        }
                        if (ShouldRemove(item.info.shortname))
                        {
                            hasRemovable = true;
                        }
                    }


                    if (!hasExcluded && hasRemovable)
                    {
                        if (config.EnableLogging)
                        {
                            var ownerID = droppedContainer.OwnerID.ToString();
                            logEntries.Add($"[{now}] Removed DroppedItemContainer, OwnerID: {ownerID}");
                        }
                        KillEntity(entity);
                        itemsRemoved++;
                    }
                }
            }

            SendLocalizedNotificationToAll("MAP_CLEANED", itemsRemoved);
            Puts($"Removed {itemsRemoved} items.");

            if (config.EnableLogging && logEntries.Count > 0)
            {
                foreach (var line in logEntries)
                {
                    WriteToLogFiles(line);
                }
            }
            playerDroppedItems.Clear();
        }

        private void KillEntity(BaseEntity entity)
        {
            if (entity == null) return;
            try
            {
                string netIdString = entity.net?.ID.ToString() ?? "0";
                entity.Kill();
                Puts($"Killed entity: {entity.ShortPrefabName} (ID: {netIdString})");
            }
            catch (Exception ex)
            {
                PrintWarning($"Error while killing entity: {ex.Message}");
            }
        }

        #endregion

        #region Removal Checks

        private bool ShouldRemove(string shortName)
        {
            if (string.IsNullOrEmpty(shortName)) return false;


            if (config.CleanItemShortNames.Contains(shortName))
                return true;


            return !IsExcludedItem(shortName);
        }

        private bool IsExcludedItem(string shortName)
        {
            return !string.IsNullOrEmpty(shortName) && config.ExcludedItemShortNames.Contains(shortName);
        }

        private bool IsParachute(BaseEntity entity)
        {
            var prefab = entity.ShortPrefabName?.ToLower() ?? "";
            return prefab.Contains("parachute.deployed") || prefab.Contains("parachuteunpacked");
        }

        private bool IsInExcludedZone(BaseEntity entity)
        {
            if (ZoneManager == null || config.ExcludedZoneIDs.Count == 0)
                return false;

            var zoneIDs = ZoneManager.Call("GetEntityZoneIDs", entity) as List<string>;
            if (zoneIDs == null || zoneIDs.Count == 0)
                return false;

            return zoneIDs.Any(z => config.ExcludedZoneIDs.Contains(z));
        }

        #endregion

        #region Notifications & Logging

        private void SendLocalizedNotificationToAll(string messageId, params object[] args)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                string message = string.Format(lang.GetMessage(messageId, this, player.UserIDString), args);
                player.ChatMessage($"{config.Prefix} {message}");
            }
        }

        private void SendNotificationToAll(string message)
        {
            bool playerNotify = config.EnablePlayerNotifications;
            bool adminNotify = config.EnableAdminNotifications;
            if (!playerNotify && !adminNotify) return;

            if (playerNotify)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    player.ChatMessage(message);
                }
            }
            if (adminNotify)
            {
                foreach (var admin in BasePlayer.activePlayerList
                         .Where(p => permission.UserHasPermission(p.UserIDString, PermAdmin)))
                {
                    admin.ChatMessage(message);
                }
            }
        }

        private void NotifyPlayers()
        {
            if (!config.EnablePlayerNotifications && !config.EnableAdminNotifications) return;

            foreach (var kvp in playerDroppedItems)
            {
                var ownerID = kvp.Key;
                var ownerPlayer = BasePlayer.FindByID(ownerID);
                if (ownerPlayer == null) continue;

                ownerPlayer.ChatMessage($"{config.Prefix} {lang.GetMessage("ITEMS_WILL_BE_CLEARED", this, ownerPlayer.UserIDString)}");

                if (config.EnableAdminNotifications)
                {
                    foreach (var admin in BasePlayer.activePlayerList
                             .Where(a => permission.UserHasPermission(a.UserIDString, PermAdmin)))
                    {
                        string msg = string.Format(lang.GetMessage("PLAYER_HAS_ITEMS", this, admin.UserIDString), ownerPlayer.displayName);
                        admin.ChatMessage($"{config.Prefix} {msg}");
                    }
                }
            }
        }

        private void WriteToLogFiles(string line)
        {
            if (!config.EnableLogging) return;
            try
            {
                var baseLogDir = Path.Combine(Interface.Oxide.LogDirectory, "RustRearmSystem", "AutoClean", "logs");
                if (!Directory.Exists(baseLogDir))
                    Directory.CreateDirectory(baseLogDir);

                string txtPath = Path.Combine(baseLogDir, "autoclean.txt");
                string logPath = Path.Combine(baseLogDir, "autoclean.log");

                File.AppendAllText(txtPath, line + Environment.NewLine);
                File.AppendAllText(logPath, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                PrintWarning($"Could not write to log files: {ex.Message}");
            }
        }

        private void EnsureDirectoriesExist()
        {
            var baseLogDir = Path.Combine(Interface.Oxide.LogDirectory, "RustRearmSystem", "AutoClean", "logs");
            if (!Directory.Exists(baseLogDir))
                Directory.CreateDirectory(baseLogDir);

            var baseDataDir = Path.Combine(Interface.Oxide.DataDirectory, "RustRearmSystem", "AutoClean", "data");
            if (!Directory.Exists(baseDataDir))
                Directory.CreateDirectory(baseDataDir);
        }

        #endregion

        #region Event Hooks

        private void OnEntityDropped(BaseEntity entity)
        {

            var dropped = entity as DroppedItem;
            if (dropped == null) return;

            ulong ownerID = dropped.OwnerID;
            if (ownerID == 0) return;

            if (!playerDroppedItems.ContainsKey(ownerID))
                playerDroppedItems[ownerID] = new List<BaseEntity>();

            playerDroppedItems[ownerID].Add(entity);
        }

        #endregion

        #region Commands

        [ChatCommand("clean")]
        private void CleanCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermUse))
            {
                player.ChatMessage($"{config.Prefix} {lang.GetMessage("NOPERM", this, player.UserIDString)}");
                return;
            }
            CleanMap();
            SendLocalizedNotificationToAll("MAP_CLEANED_BY_ADMIN");
        }

        #endregion

        #region Localization

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MAP_WILL_BE_CLEARED"] = "Map will be cleared in {0} minute(s). Pick up anything you need.",
                ["MAP_CLEANED"] = "Map cleanup complete. Removed {0} item(s).",
                ["ITEMS_WILL_BE_CLEARED"] = "You have items on the ground that will be cleaned soon.",
                ["PLAYER_HAS_ITEMS"] = "Player {0} has items on the ground that will be cleaned soon.",
                ["MAP_CLEANED_BY_ADMIN"] = "Map was cleared by an administrator to maintain performance.",
                ["NOPERM"] = "You do not have permission to use this command!"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MAP_WILL_BE_CLEARED"] = "Через {0} минут(ы) карта будет очищена. Заберите нужное.",
                ["MAP_CLEANED"] = "Очистка карты завершена. Удалено {0} предмет(ов).",
                ["ITEMS_WILL_BE_CLEARED"] = "У вас на земле лежат предметы, которые будут удалены.",
                ["PLAYER_HAS_ITEMS"] = "У игрока {0} есть предметы на земле, которые будут удалены.",
                ["MAP_CLEANED_BY_ADMIN"] = "Карту очистил администратор для поддержания производительности.",
                ["NOPERM"] = "У вас нет прав для использования этой команды!"
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MAP_WILL_BE_CLEARED"] = "Через {0} хвилин(и) карта буде очищена. Заберіть необхідне.",
                ["MAP_CLEANED"] = "Очищення карти завершено. Видалено {0} предмет(ів).",
                ["ITEMS_WILL_BE_CLEARED"] = "У вас на землі лежать предмети, які будуть видалені.",
                ["PLAYER_HAS_ITEMS"] = "У гравця {0} є предмети на землі, які будуть видалені.",
                ["MAP_CLEANED_BY_ADMIN"] = "Карту очистив адміністратор для підтримання продуктивності.",
                ["NOPERM"] = "У вас немає прав для використання цієї команди!"
            }, this, "uk");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MAP_WILL_BE_CLEARED"] = "El mapa se limpiará en {0} minuto(s). Recoge lo que necesites.",
                ["MAP_CLEANED"] = "Limpieza del mapa completada. Se eliminaron {0} objeto(s).",
                ["ITEMS_WILL_BE_CLEARED"] = "Tienes objetos en el suelo que serán limpiados.",
                ["PLAYER_HAS_ITEMS"] = "El jugador {0} tiene objetos en el suelo que serán limpiados.",
                ["MAP_CLEANED_BY_ADMIN"] = "El mapa fue limpiado por un administrador para mantener el rendimiento.",
                ["NOPERM"] = "¡No tienes permiso para usar este comando!"
            }, this, "es-ES");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MAP_WILL_BE_CLEARED"] = "Die Karte wird in {0} Minute(n) geleert. Hebe alles auf, was du brauchst.",
                ["MAP_CLEANED"] = "Kartenreinigung abgeschlossen. {0} Gegenstand(e) entfernt.",
                ["ITEMS_WILL_BE_CLEARED"] = "Du hast Gegenstände auf dem Boden, die bald entfernt werden.",
                ["PLAYER_HAS_ITEMS"] = "Spieler {0} hat Gegenstände auf dem Boden, die entfernt werden.",
                ["MAP_CLEANED_BY_ADMIN"] = "Die Karte wurde vom Administrator zur Leistungsverbesserung gereinigt.",
                ["NOPERM"] = "Du hast keine Berechtigung, diesen Befehl zu verwenden!"
            }, this, "de");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["MAP_WILL_BE_CLEARED"] = "La carte sera nettoyée dans {0} minute(s). Ramassez ce dont vous avez besoin.",
                ["MAP_CLEANED"] = "Nettoyage de la carte terminé. {0} objet(s) supprimé(s).",
                ["ITEMS_WILL_BE_CLEARED"] = "Vous avez des objets au sol qui seront bientôt supprimés.",
                ["PLAYER_HAS_ITEMS"] = "Le joueur {0} a des objets au sol qui seront bientôt supprimés.",
                ["MAP_CLEANED_BY_ADMIN"] = "La carte a été nettoyée par un administrateur pour maintenir les performances.",
                ["NOPERM"] = "Vous n'avez pas la permission d'utiliser cette commande!"
            }, this, "fr");
        }

        #endregion
    }
}
