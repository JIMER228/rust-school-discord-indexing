using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("Remove", "OxideBro", "1.5.25")]
    class Remove : RustPlugin
    {
        static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0);
        static double CurrentTime() => DateTime.UtcNow.Subtract(epoch).TotalSeconds;

        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Благодарим за покупку плагина на у разработчика OxideBro. Если вы передадите этот плагин сторонним лицам знайте - это лишает вас гарантированных обновлений!");
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
            PluginConfig config = PluginConfig.DefaultConfig();
            if (config.PluginVersion < new VersionNumber(1, 5, 25))
            {
                PrintWarning("Config update detected! Updating config values...");
                PrintWarning("Config update completed!");
            }
            config.PluginVersion = Version;
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        public class MainSettings
        {
            [JsonProperty("Включить выключение авто-улучшения при включении режима удаления (Поддержка плагина BuildingUpgrade с сайта RustPlugin.ru)")]
            public bool EnabledBuildingUpgrade;
            [JsonProperty("Время действия режима удаления")]
            public int resetTime;
            [JsonProperty("Процент возвращаемых ресурсов с построек (Максимум 1.0 - это 100%)")]
            public float refundPercent;
            [JsonProperty("Процент возвращаемых ресурсов с Items (Максимум 1.0 - это 100%)")]
            public float refundItemsPercent;
            [JsonProperty("Процент выпадающих ресурсов (не вещей) с удаляемых ящиков (Максимум 1.0 - это 100%)")]
            public float refundStoragePercent;
            [JsonProperty("Включить запрет на удаление объекта если в его инвентаре есть предметы")]
            public bool CheckRemoveItems;
            [JsonProperty("Разрешить удаление чужих объектов при наличии авторизации в шкафу")]
            public bool cupboardRemove;
            [JsonProperty("Разрешить удаление собственных объектов без авторизации в шкафу")]
            public bool selfRemove;
            [JsonProperty("Разрешить удаление обьектов друзьям")]
            public bool removeFriends;
            [JsonProperty("Разрешить удаление объектов соклановцев")]
            public bool removeClans;
            [JsonProperty("Разрешить удаление обьектов команде игрока (Team)")]
            public bool removeTeam;
            [JsonProperty("Включить возрат объектов (При удаление объектов(сундуки, печки и тд.) будет возращать объект а не ресурсы)")]
            public bool refundItemsGive;
            [JsonProperty("Включить потерю прочности предмета (Item lose condition)")]
            public bool EnableLoseCondition;
            [JsonProperty("Сколько процентов прочности теряет предмет при удалении (дефолт - 10 (250 - 10% = 25))")]
            public int GetLoseCondition;
            [JsonProperty("Включить поддержку NoEscape (С сайта RustPlugin.ru)")]
            public bool useNoEscape;
            [JsonProperty("Включить запрет на удаление объекта для игрока после истечения N времени указанным в конфигурации")]
            public bool EnTimedRemove;
            [JsonProperty("Время на запрет удаление объекта после истечения указаного времени (в секундах)")]
            public float Timeout;
            [JsonProperty("Привилегия игнорирования запрета удаления объектов какие были установлены N времени назад (Если включено)")]
            public string IgnorePrivilage;
            [JsonProperty("Список запрещенных для удаления Entity shortname (Не Item)")]
            public List<string> BlackListed = new List<string>();

        }

        public class GUISettings
        {
            [JsonProperty("Панель AnchorMin")]
            public string PanelAnchorMin;
            [JsonProperty("Панель AnchorMax")]
            public string PanelAnchorMax;
            [JsonProperty("Цвет фона")]
            public string PanelColor;
            [JsonProperty("Размер текста")]
            public int TextFontSize;
            [JsonProperty("Цвет текста")]
            public string TextСolor;
            [JsonProperty("Текст AnchorMin")]
            public string TextAnchorMin;
            [JsonProperty("Текст AnchorMax")]
            public string TextAnchorMax;
        }


        class PluginConfig
        {


            [JsonProperty("Основные")]
            public MainSettings mainSettings;

            [JsonProperty("GUI")]
            public GUISettings gUISettings;

            [JsonProperty("Configuration Version")]
            public VersionNumber PluginVersion = new VersionNumber();
            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    PluginVersion = new VersionNumber(),
                    mainSettings = new MainSettings()
                    {
                        BlackListed = new List<string>()
                        {
                            ""
                        },
                        CheckRemoveItems = false,
                        cupboardRemove = true,
                        EnabledBuildingUpgrade = true,
                        selfRemove = false,
                        refundStoragePercent = 1,
                        EnTimedRemove = false,
                        IgnorePrivilage = "remove.ignore",
                        refundItemsGive = true,
                        refundItemsPercent = 1,
                        refundPercent = 1,
                        removeClans = false,
                        removeFriends = false,
                        removeTeam = true,
                        resetTime = 30,
                        Timeout = 3600,
                        useNoEscape = false,
                        EnableLoseCondition = true,
                        GetLoseCondition = 10

                    },
                    gUISettings = new GUISettings()
                    {
                        PanelAnchorMax = "1 0.958",
                        TextСolor = "0 0 0 1",
                        PanelAnchorMin = "0.0 0.908",
                        PanelColor = "0 0 0 0.50",
                        TextAnchorMax = "1 1",
                        TextAnchorMin = "0 0",
                        TextFontSize = 14

                    }
                };
            }
        }

        static int constructionColl = LayerMask.GetMask(new string[] {
            "Construction", "Deployable", "Prevent Building", "Deployed"
        }
        );
        private static Dictionary<string, int> deployedToItem = new Dictionary<string, int>();
        Dictionary<BasePlayer, int> timers = new Dictionary<BasePlayer, int>();
        Dictionary<ulong, string> activePlayers = new Dictionary<ulong, string>();
        int currentRemove = 0;
        [PluginReference] Plugin Clans, BuildingUpgrade, Friends, NoEscape;
        bool IsClanMember(ulong playerid, ulong targetID)
        {
            if (plugins.Exists("Clans"))
            {
                var result = Clans?.Call("IsTeammate", playerid, targetID) != null ? Clans?.Call("IsTeammate", playerid, targetID) : (bool)(Clans?.Call("HasFriend", playerid, targetID) ?? false);
                if (result != null)
                    return (bool)result;
            }
            return false;
        }
        bool IsFriends(ulong playerid = 0, ulong friendId = 0)
        {
            if (plugins.Exists("Friends"))
            {
                return (bool)(Friends?.Call("AreFriends", playerid, friendId) ?? false);
            }
            return false;
        }

        bool IsTeamate(BasePlayer player, ulong targetID)
        {
            if (player.currentTeam == 0) return false;
            var team = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
            if (team == null) return false;
            return team.members.Contains(targetID);
        }

        private Dictionary<ulong, DateTime> Cooldowns = new Dictionary<ulong, DateTime>();
        private double Cooldown = 30f;

        object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null) return null;
            if (activePlayers.ContainsKey(player.userID))
            {
                timers.Remove(player);
                DeactivateRemove(player.userID);
                DestroyUI(player);
            }
            return null;
        }

        void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (player == null) return;
            if (config.mainSettings.EnTimedRemove)
            {
                if (newItem == null) return;
                if (newItem.info.shortname == "building.planner")
                {
                    if (Cooldowns.ContainsKey(player.userID))
                    {
                        double seconds = Cooldowns[player.userID].Subtract(DateTime.Now).TotalSeconds;
                        if (seconds >= 0) return;
                    }
                    SendReply(player, Messages["enabledRemoveTimer"], NumericalFormatter.FormatTime(config.mainSettings.Timeout));
                    Cooldowns[player.userID] = DateTime.Now.AddSeconds(Cooldown);
                }
            }

        }


        [ChatCommand("remove")]
        void cmdRemove(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, "remove.use"))
            {
                SendReply(player, Messages["NoPermission"]);
                return;
            }




            if (config.mainSettings.EnabledBuildingUpgrade && BuildingUpgrade && BuildingUpgrade?.Call("BuildingUpgradeActivate", player.userID) != null)
            {
                var upgradeEnabled = (bool)BuildingUpgrade?.Call("BuildingUpgradeActivate", player.userID);
                if (upgradeEnabled)
                    BuildingUpgrade?.Call("BuildingUpgradeDeactivate", player.userID);
            }

            if (args == null || args.Length == 0)
            {
                if (activePlayers.ContainsKey(player.userID))
                {
                    timers.Remove(player);
                    DeactivateRemove(player.userID);
                    DestroyUI(player);
                    return;
                }
                else
                {
                    var messages =  Messages["enabledRemove"];
                    SendReply(player, messages);
                    timers[player] = config.mainSettings.resetTime;
                    DrawUI(player, config.mainSettings.resetTime, "normal");
                    ActivateRemove(player.userID, "normal");
                    return;
                }
            }

            switch (args[0])
            {
                case "admin":
                    if (!permission.UserHasPermission(player.UserIDString, "remove.admin"))
                    {
                        SendReply(player, Messages["NoPermission"]);
                        return;
                    }
                    if (activePlayers.ContainsKey(player.userID))
                    {
                        timers.Remove(player);
                        DeactivateRemove(player.userID);
                        DestroyUI(player);
                        return;
                    }
                    timers[player] = config.mainSettings.resetTime;
                    DrawUI(player, config.mainSettings.resetTime, "admin");
                    ActivateRemove(player.userID, "admin");
                    break;
                case "all":
                    if (!permission.UserHasPermission(player.UserIDString, "remove.admin"))
                    {
                        SendReply(player, Messages["NoPermission"]);
                        return;
                    }
                    if (activePlayers.ContainsKey(player.userID))
                    {
                        timers.Remove(player);
                        DeactivateRemove(player.userID);
                        DestroyUI(player);
                        return;
                    }
                    timers[player] = config.mainSettings.resetTime;
                    DrawUI(player, config.mainSettings.resetTime, "all");
                    ActivateRemove(player.userID, "all");
                    break;
            }
        }

        [ConsoleCommand("remove.toggle")]
        void cmdConsoleRemove(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, "remove.use"))
            {
                SendReply(player, Messages["NoPermission"]);
                return;
            }
            if (config.mainSettings.EnabledBuildingUpgrade && BuildingUpgrade && (bool)BuildingUpgrade?.Call("BuildingUpgradeActivate", player.userID))
                BuildingUpgrade?.Call("BuildingUpgradeDeactivate", player.userID);


            if (args.Args == null || args.Args.Length == 0)
            {
                if (activePlayers.ContainsKey(player.userID))
                {
                    timers.Remove(player);
                    DeactivateRemove(player.userID);
                    DestroyUI(player);
                    return;
                }
                else
                {
                    var messages =  Messages["enabledRemove"];
                    SendReply(player, messages);
                    timers[player] = config.mainSettings.resetTime;
                    DrawUI(player, config.mainSettings.resetTime, "normal");
                    ActivateRemove(player.userID, "normal");
                    return;
                }
            }
            switch (args.Args[0])
            {
                case "admin":
                    if (!permission.UserHasPermission(player.UserIDString, "remove.admin"))
                    {
                        SendReply(player, Messages["NoPermission"]);
                        return;
                    }
                    if (activePlayers.ContainsKey(player.userID))
                    {
                        timers.Remove(player);
                        DeactivateRemove(player.userID);
                        DestroyUI(player);
                        return;
                    }
                    timers[player] = config.mainSettings.resetTime;
                    DrawUI(player, config.mainSettings.resetTime, "admin");
                    ActivateRemove(player.userID, "admin");
                    break;
                case "all":
                    if (!permission.UserHasPermission(player.UserIDString, "remove.admin"))
                    {
                        SendReply(player, Messages["NoPermission"]);
                        return;
                    }
                    if (activePlayers.ContainsKey(player.userID))
                    {
                        timers.Remove(player);
                        DeactivateRemove(player.userID);
                        DestroyUI(player);
                        return;
                    }
                    timers[player] = config.mainSettings.resetTime;
                    DrawUI(player, config.mainSettings.resetTime, "all");
                    ActivateRemove(player.userID, "all");
                    break;
            }
        }

        Dictionary<uint, double> entityes = new Dictionary<uint, double>();

        void LoadEntity()
        {
            if (!config.mainSettings.EnTimedRemove) return;
            try
            {
                entityes = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<uint, double>>("Remove_NewEntity");
            }
            catch
            {
                entityes = new Dictionary<uint, double>();
            }
        }

        void CheckEntity()
        {
            if (entityes == null)
            {
                entityes = new Dictionary<uint, double>();
                return;
            }
            var entityList = entityes.Keys.ToList();
            for (int i = 0; i < entityList.Count; i++)
            {
                var Entity = BaseEntity.serverEntities.Find(entityList[i]);
                if (Entity == null)
                    entityes.Remove(entityList[i]);
                else
                {
                    var cTime = entityList[i];
                    if ((CurrentTime() - cTime) < config.mainSettings.Timeout)
                        entityes.Remove(entityList[i]);
                }
            }
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (plan == null || go == null) return;
            if (config.mainSettings.EnTimedRemove)
            {
                BaseEntity entity = go.ToBaseEntity();
                if (entity?.net?.ID == null) return;
                entityes.Add(entity.net.ID, CurrentTime());
            }
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null || entity?.net?.ID == null) return;
            if (entityes.ContainsKey(entity.net.ID))
                entityes.Remove(entity.net.ID);
        }

        void OnNewSave()
        {
            if (config.mainSettings.EnTimedRemove)
            {
                Puts("Обнаружен вайп. Очищаем сохраненные объекты");
                entityes = new Dictionary<uint, double>();
                SaveData();
            }
        }

        void SaveData()
        {
            if (entityes != null && config.mainSettings.EnTimedRemove)
                Interface.Oxide.DataFileSystem.WriteObject("Remove_NewEntity", entityes);
        }

        void OnServerSave()
        {
            SaveData();
        }

        void Loaded()
        {
            if (config.mainSettings.EnTimedRemove)
            {
                Subscribe("OnEntityBuilt");
                Subscribe("OnEntityKill");
                Subscribe("OnServerSave");
            }
            else
            {
                Unsubscribe("OnEntityBuilt");
                Unsubscribe("OnEntityKill");
                Unsubscribe("OnServerSave");
            }
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            permission.RegisterPermission("remove.admin", this);
            permission.RegisterPermission("remove.use", this);
            permission.RegisterPermission(config.mainSettings.IgnorePrivilage, this);
            LoadEntity();

        }


        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }

            SaveData();
        }


        void OnServerInitialized()
        {
            CheckEntity();
            if (config.mainSettings.removeFriends)
            {
                if (!plugins.Exists("Friends"))
                {
                    PrintWarning("Plugin Friends not found. Remove of friends buildings is disabled");
                    config.mainSettings.removeFriends = false;
                }
            }
            if (config.mainSettings.removeClans)
            {
                if (!plugins.Exists("Clans"))
                {
                    PrintWarning("Plugin Clans not found. Remove of clans buildings is disabled");
                    config.mainSettings.removeClans = false;
                }
            }

            List<ItemDefinition> ItemsDefinition = ItemManager.GetItemDefinitions() as List<ItemDefinition>;
            foreach (ItemDefinition itemdef in ItemsDefinition)
            {
                if (itemdef?.GetComponent<ItemModDeployable>() == null) continue;
                if (deployedToItem.ContainsKey(itemdef.GetComponent<ItemModDeployable>().entityPrefab.resourcePath)) continue;

                deployedToItem.Add(itemdef.GetComponent<ItemModDeployable>().entityPrefab.resourcePath, itemdef.itemid);
            }

            timer.Every(1f, TimerHandler);
        }

        void RemoveAllFrom(Vector3 pos)
        {
            removeFrom.Add(pos);
            DelayRemoveAll();
        }
        List<BaseEntity> wasRemoved = new List<BaseEntity>();
        List<Vector3> removeFrom = new List<Vector3>();

        void DelayRemoveAll()
        {
            if (currentRemove >= removeFrom.Count)
            {
                currentRemove = 0;
                removeFrom.Clear();
                wasRemoved.Clear();
                return;
            }
            List<BaseEntity> list = Pool.GetList<BaseEntity>();
            Vis.Entities<BaseEntity>(removeFrom[currentRemove], 3f, list, constructionColl);
            for (int i = 0;
            i < list.Count;
            i++)
            {
                BaseEntity ent = list[i];
                if (wasRemoved.Contains(ent)) continue;
                if (!removeFrom.Contains(ent.transform.position)) removeFrom.Add(ent.transform.position);
                wasRemoved.Add(ent);
                DoRemove(ent);
            }
            currentRemove++;
            timer.Once(0.01f, () => DelayRemoveAll());
        }

        static void DoRemove(BaseEntity removeObject)
        {
            if (removeObject == null) return;
            StorageContainer Container = removeObject.GetComponent<StorageContainer>();
            if (Container != null)
            {
                DropUtil.DropItems(Container.inventory, removeObject.transform.position);
            }
            EffectNetwork.Send(new Effect("assets/bundled/prefabs/fx/item_break.prefab", removeObject, 0, Vector3.up, Vector3.zero)
            {
                scale = UnityEngine.Random.Range(0f, 1f)
            }
            );
            removeObject.KillMessage();
        }

        void TryRemove(BasePlayer player, BaseEntity removeObject)
        {
            RemoveAllFrom(removeObject.transform.position);
        }


        public List<string> HummersPrefabs = new List<string>()
        {
            "assets/prefabs/weapons/hammer/hammer.entity.prefab",
            "assets/prefabs/weapons/toolgun/toolgun.entity.prefab"
        };

        object OnMeleeAttack(BasePlayer player, HitInfo info)
        {
            if (info == null || info == null) return null;
            var entity = info?.HitEntity;
            if (entity == null) return null;
            var hummer = info?.WeaponPrefab?.name;
            if (string.IsNullOrEmpty(hummer) || !HummersPrefabs.Contains(hummer)) return null;
            if (entity.IsDestroyed || entity.OwnerID == 0 || !activePlayers.ContainsKey(player.userID)) return null;
            switch (activePlayers[player.userID])
            {
                case "all":
                    TryRemove(player, info.HitEntity);
                    var pos = player.transform.position;
                    RemoveEntityAll(player, entity, pos);
                    return true;
                case "admin":
                    RemoveEntityAdmin(player, entity);
                    return true;
                case "normal":
                    if (entity.ShortPrefabName.Contains("recycler")) return null;
                    if (config.mainSettings.BlackListed.Contains(entity.ShortPrefabName))
                    {
                        SendReply(player, Messages["EntityBlackListed"]);
                        return false;
                    }
                    if (entity.GetComponent<ItemModDeployable>() != null && !deployedToItem.ContainsKey(entity.PrefabName)) return null;

                    var externalPlugins = Interface.CallHook("canRemove", player, entity);
                    if (externalPlugins != null)
                    {
                        SendReply(player, Messages["RET"]);
                        return false;
                    }

                    if (entity.GetComponent<StorageContainer>() != null && config.mainSettings.CheckRemoveItems)
                    {
                        StorageContainer storage = entity.GetComponent<StorageContainer>();

                        if (storage.inventory != null && storage.inventory.itemList.Count > 0)
                        {
                            SendReply(player, Messages["CheckItems"]);
                            return false;
                        }
                    }

                    if (NoEscape && config.mainSettings.useNoEscape)
                    {
                        var isRaid = (bool)NoEscape?.Call("IsRaidBlocked", player);
                        if (isRaid)
                        {
                            SendReply(player, Messages["NoEscape"]);
                            return false;
                        }
                    }
                    var privilege = entity.GetBuildingPrivilege();
                    if (privilege != null)
                    {
                        if (privilege.IsAuthed(player))
                        {
                            if (entity.OwnerID == player.userID)
                            {
                                RemoveEntity(player, entity);
                                return true;
                            }

                            if (config.mainSettings.cupboardRemove)
                            {
                                RemoveEntity(player, entity);
                                return true;
                            }

                            if (config.mainSettings.removeTeam && IsTeamate(player, entity.OwnerID))
                            {
                                RemoveEntity(player, entity);
                                return true;
                            }

                            if (config.mainSettings.removeFriends && IsFriends(entity.OwnerID, player.userID))
                            {
                                RemoveEntity(player, entity);
                                return true;
                            }
                            if (config.mainSettings.removeClans && IsClanMember(entity.OwnerID, player.userID))
                            {
                                RemoveEntity(player, entity);
                                return true;
                            }
                            SendReply(player, Messages["norights"]);
                            return false;
                        }
                        else
                        {
                            if (config.mainSettings.selfRemove && entity.OwnerID == player.userID)
                            {
                                RemoveEntity(player, entity);
                                return true;
                            }

                            SendReply(player, Messages["ownerCup"]);
                            return false;
                        }
                    }
                    else
                    {
                        if (entity.OwnerID == player.userID)
                        {
                            RemoveEntity(player, entity);
                            return true;
                        }
                        if (config.mainSettings.removeTeam && IsTeamate(player, entity.OwnerID))
                        {
                            RemoveEntity(player, entity);
                            return true;
                        }

                        if (config.mainSettings.removeFriends && IsFriends(entity.OwnerID, player.userID))
                        {
                            RemoveEntity(player, entity);
                            return true;
                        }
                        if (config.mainSettings.removeClans && IsClanMember(entity.OwnerID, player.userID))
                        {
                            RemoveEntity(player, entity);
                            return true;
                        }
                        SendReply(player, Messages["norights"]);
                        return false;
                    }
            }
            return null;
        }
        
        public static class NumericalFormatter
        {
            private static string GetNumEndings(int origNum, string[] forms)
            {
                string result;
                var num = origNum % 100;
                if (num >= 11 && num <= 19)
                {
                    result = forms[2];
                }
                else
                {
                    num = num % 10;
                    switch (num)
                    {
                        case 1: result = forms[0]; break;
                        case 2:
                        case 3:
                        case 4:
                            result = forms[1]; break;
                        default:
                            result = forms[2]; break;
                    }
                }
                return string.Format("{0} {1} ", origNum, result);
            }

            private static string FormatSeconds(int seconds) =>
                GetNumEndings(seconds, new[] { "секунду", "секунды", "секунд" });
            private static string FormatMinutes(int minutes) =>
                GetNumEndings(minutes, new[] { "минуту", "минуты", "минут" });
            private static string FormatHours(int hours) =>
                GetNumEndings(hours, new[] { "час", "часа", "часов" });
            private static string FormatDays(int days) =>
                GetNumEndings(days, new[] { "день", "дня", "дней" });
            private static string FormatTime(TimeSpan timeSpan)
            {
                string result = string.Empty;
                if (timeSpan.Days > 0)
                    result += FormatDays(timeSpan.Days);
                if (timeSpan.Hours > 0)
                    result += FormatHours(timeSpan.Hours);
                if (timeSpan.Minutes > 0)
                    result += FormatMinutes(timeSpan.Minutes);
                if (timeSpan.Seconds > 0)
                    result += FormatSeconds(timeSpan.Seconds).TrimEnd(' ');
                return result;
            }
            public static string FormatTime(int seconds) => FormatTime(new TimeSpan(0, 0, seconds));
            public static string FormatTime(float seconds) => FormatTime((int)Math.Round(seconds));
            public static string FormatTime(double seconds) => FormatTime((int)Math.Round(seconds));
        }

        void TimerHandler()
        {
            foreach (var player in timers.Keys.ToList())
            {
                var seconds = --timers[player];
                if (seconds <= 0)
                {
                    timers.Remove(player);
                    DeactivateRemove(player.userID);
                    DestroyUI(player);
                    continue;
                }
                DrawUI(player, seconds, activePlayers[player.userID]);
            }
        }

        void RemoveEntity(BasePlayer player, BaseEntity entity)
        {
            if (config.mainSettings.EnTimedRemove && !permission.UserHasPermission(player.UserIDString, config.mainSettings.IgnorePrivilage))
            {
                if (entityes.ContainsKey(entity.net.ID))
                {
                    var cTime = entityes[entity.net.ID];
                    if ((CurrentTime() - cTime) > config.mainSettings.Timeout)
                    {
                        SendReply(player, Messages["blockremovetime"], NumericalFormatter.FormatTime(config.mainSettings.Timeout));
                        return;
                    }
                }
                else
                {
                    SendReply(player, Messages["blockremovetime"], NumericalFormatter.FormatTime(config.mainSettings.Timeout));
                    return;

                }
            }

            if (entity is BuildingBlock)
            {
                var BEntity = entity.GetComponent<BuildingBlock>();

                object obj = Interface.CallHook("OnStructureDemolish", BEntity, player, false);
                if (obj is bool)
                {
                    SendReply(player, Messages["BlockDemolosh"]);
                    return;
                }
            }
            Refund(player, entity);
            entity.Kill(BaseNetworkable.DestroyMode.Gib);
            UpdateTimer(player, "normal");
        }

        void RemoveEntityAdmin(BasePlayer player, BaseEntity entity)
        {
            entity.Kill();
            UpdateTimer(player, "admin");
        }

        void RemoveEntityAll(BasePlayer player, BaseEntity entity, Vector3 pos)
        {
            removeFrom.Add(pos);
            DelayRemoveAll();
            UpdateTimer(player, "all");
        }

        void Refund(BasePlayer player, BaseEntity entity)
        {
            if (entity is BuildingBlock)
            {
                BuildingBlock buildingblock = entity as BuildingBlock;
                if (buildingblock.blockDefinition == null) return;
                int buildingblockGrade = (int)buildingblock.grade;
                if (buildingblock.blockDefinition.grades[buildingblockGrade] != null)
                {
                    float refundRate = buildingblock.healthFraction * config.mainSettings.refundPercent;
                    List<ItemAmount> currentCost = buildingblock.blockDefinition.grades[buildingblockGrade].costToBuild as List<ItemAmount>;
                    foreach (ItemAmount ia in currentCost)
                    {
                        int amount = (int)(ia.amount * refundRate);
                        if (amount <= 0 || amount > ia.amount || amount >= int.MaxValue) amount = 1;
                        if (refundRate != 0)
                        {
                            Item x = ItemManager.CreateByItemID(ia.itemid, amount);
                            player.GiveItem(x, BaseEntity.GiveItemReason.PickedUp);
                        }
                    }
                }
                return;
            }
            StorageContainer storage = entity as StorageContainer;
            if (storage)
            {
                if (storage.inventory.itemList.Count > 0) for (int i = storage.inventory.itemList.Count - 1;
                i >= 0;
                i--)
                    {
                        var item = storage.inventory.itemList[i];
                        if (item == null) continue;
                        if (item.info.shortname == "water") continue;
                        item.amount = (int)(item.amount * config.mainSettings.refundStoragePercent);
                        float single = 20f;
                        Vector3 vector32 = Quaternion.Euler(UnityEngine.Random.Range(-single * 0.1f, single * 0.1f), UnityEngine.Random.Range(-single * 0.1f, single * 0.1f), UnityEngine.Random.Range(-single * 0.1f, single * 0.1f)) * Vector3.up;
                        BaseEntity baseEntity = item.Drop(storage.transform.position + (Vector3.up * 0f), vector32 * UnityEngine.Random.Range(5f, 10f), UnityEngine.Random.rotation);
                        baseEntity.SetAngularVelocity(UnityEngine.Random.rotation.eulerAngles * 5f);
                    }
            }
            if (deployedToItem.ContainsKey(entity.gameObject.name))
            {
                if (entity.children != null && entity.children.Count > 0)
                {
                    foreach (var ent in entity.children.Where(p => deployedToItem.ContainsKey(p.PrefabName)))
                        GiveAndShowItem(player, deployedToItem[ent.PrefabName], 1, 100);
                }
                ItemDefinition def = ItemManager.FindItemDefinition(deployedToItem[entity.gameObject.name]);
                if (def == null) return;
                if (!config.mainSettings.refundItemsGive && def.Blueprint != null)
                {
                    foreach (var ingredient in def.Blueprint.ingredients)
                    {
                        var reply = 0;
                        if (reply == 0) { }
                        var amountOfIngridient = ingredient.amount;
                        var amount = Mathf.Floor(amountOfIngridient * config.mainSettings.refundItemsPercent);
                        if (amount <= 0 || amount > amountOfIngridient) amount = 1;
                        if (config.mainSettings.refundItemsPercent > 0)
                        {
                            Item x = ItemManager.Create(ingredient.itemDef, (int)amount);
                            player.GiveItem(x, BaseEntity.GiveItemReason.PickedUp);
                        }
                    }
                }
                else
                {
                    var height = entity.Health() * 100 / entity.MaxHealth();
                    if (config.mainSettings.EnableLoseCondition) height -= config.mainSettings.GetLoseCondition;
                    GiveAndShowItem(player, deployedToItem[entity.PrefabName], 1, height);
                }
            }
        }

        private bool OnRemoveActivate(ulong id)
        {
            return activePlayers.ContainsKey(id);
        }

        private void RemoveDeativate(ulong player)
        {
            if (activePlayers.ContainsKey(player))
            {
                var pl = BasePlayer.FindByID(player);
                if (pl != null)
                {
                    timers.Remove(pl);
                    DeactivateRemove(pl.userID);
                    DestroyUI(pl);
                }
            }
        }

        void GiveAndShowItem(BasePlayer player, int item, int amount, float height)
        {
            Item x = ItemManager.CreateByItemID(item, amount);
            if (x.hasCondition)
            {
                if (x.condition > 1)
                    x.condition = height;
                else
                {
                    height = height / 100;
                    x.condition = height;
                }
                x.MarkDirty();
            }
            player.GiveItem(x, BaseEntity.GiveItemReason.PickedUp);
        }

        void DrawUI(BasePlayer player, int seconds, string type = "‌‌‍‍‍​​")
        {
            DestroyUI(player);

            CuiElementContainer container = new CuiElementContainer();

            var msg = type == "normal" ? Messages["RNormal"] : type == "admin" ? Messages["RAdmin"] : Messages["RAll"];
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = config.gUISettings.PanelAnchorMin, AnchorMax = config.gUISettings.PanelAnchorMax },
                Image = { Color = config.gUISettings.PanelColor }
            }, "Hud", "remove.panel");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = config.gUISettings.TextAnchorMin, AnchorMax = config.gUISettings.TextAnchorMax, OffsetMax = "0 0" },
                Text =
                    {
                        Text     = msg.Replace("{1}", NumericalFormatter.FormatTime(seconds)),
                        Align    = TextAnchor.MiddleCenter,
                        FontSize = config.gUISettings.TextFontSize,
                        Color = $"0.9 0.9 0.9 1"
                    }
            }, "remove.panel");
            CuiHelper.AddUi(player, container);

        }
        void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "remove.panel");
            CuiHelper.DestroyUi(player, "remove.text");
        }

        void ActivateRemove(ulong userId, string type)
        {
            if (!activePlayers.ContainsKey(userId))
            {
                activePlayers.Add(userId, type);
            }
        }
        void DeactivateRemove(ulong userId)
        {
            if (activePlayers.ContainsKey(userId))
            {
                activePlayers.Remove(userId);
            }
        }
        void UpdateTimer(BasePlayer player, string type)
        {
            timers[player] = config.mainSettings.resetTime;
            DrawUI(player, timers[player], type);
        }

        Dictionary<string, string> Messages = new Dictionary<string, string>() {
                {
                "NoEscape", "Удаление построек во время рейдблока запрещёно!"
            }
            , {
                "blockremovetime", "Извините, но этот объект уже нельзя удалить, он был создан более чем <color=orange>{0}</color> назад"
            }
            , {
                "NoPermission", "У Вас нету прав на использование этой команды"
            }
            , {
                "enabledRemove", "<size=16>Используйте киянку для удаления объектов</size>"
            }
            , {
                "enabledRemoveTimer", "<color=orange>Внимание:</color> Объекты созданые более чем <color=orange>{0}</color> назад, удалить нельзя"
            }
            , {
                "ownerCup", "Что бы удалять постройки, вы должны быть авторизированы в шкафу"
            }
            , {
                "norights", "Вы не имеете права удалять чужие постройки!"
            }
            , {
                "RNormal", "Режим удаления выключится через <color=orange>{1}</color>"
            }
            , {
                "RAdmin", "Режим админ удаления выключится через <color=orange>{1}</color>"
            }
            , {
                "RAll", "Режим удаления всех объектов выключится через <color=orange>{1}</color>"
            }
            , {
                "RET", "Не удалось использовать удаление постройки: внешний фактор заблокировал его использование"
            }
            , {
                "CheckItems", "Не удалось использовать удаление предмета: в его инвентаре есть предметы, очистите их"
            }
            , {
                "EntityBlackListed", "Не удалось использовать удаление предмета: он находиться в черном списке."
            }
            ,{
                "EnabledHammer", "Нажмите <color=orange>'7'</color> чтобы взять киянку для удаления предметов в руки"
            },{
                "BlockDemolosh", "Нельзя удалить данную постройку."
            }
        }
        ;
    }
}