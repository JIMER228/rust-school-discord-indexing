using Facepunch;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("Remove", "Ernieleo sell", "1.3.0")]
	[Description("Спасибо за приобретение на Oxide-Russia.ru")]
    class Remove : RustPlugin
    {
        static Remove inst;
        int resetTime = 40;
        float refundPercent = 1.0f;
        float refundItemsPercent = 1.0f;
        float refundStoragePercent = 1.0f;
        bool friendRemove;
        bool clanRemove;
        bool EnTimedRemove;
        bool CheckRemoveItems;
        bool cupboardRemove;
        bool selfRemove;
        bool removeFriends;
        bool removeClans;
        bool refundItemsGive;
        bool EnabledHammer;
		ulong HammerSkinID = 2018289599;
        float Timeout = 3600.0f;
        private string PanelAnchorMin = "0.0 0.908";
        private string PanelAnchorMax = "1 0.958";
        private string PanelColor = "0 0 0 0.50";
        private bool useNoEscape;
        private int TextFontSize = 18;
        private string TextСolor = "0 0 0 1";
        private string TextAnchorMin = "0 0";
        private string TextAnchorMax = "1 1";
        private string IgnorePrivilage = "remove.ignore";
        private bool EnabledBuildingUpgrade;
        bool removeTeam = false;

        public List<string> BlackListed = new List<string>();
        protected override void LoadDefaultConfig()
        {
            GetVariable("Основные", "Время действия режима удаления", ref resetTime);
            GetVariable("Основные", "Включить выключение авто-улучшения при включении режима удаления (Поддержка плагина BuildingUpgrade с RP)", ref EnabledBuildingUpgrade);
            GetVariable("Основные", "Включить запрет на удаление объекта для игрока после истечения N времени указанным в конфигурации", ref EnTimedRemove);
            GetVariable("Основные", "Включить запрет на удаление объекта если в его инвентаре есть предметы", ref CheckRemoveItems);
            GetVariable("Основные", "Время на запрет удаление объекта после истечения указаного времени (в секундах)", ref Timeout);
            GetVariable("Основные", "Процент возвращаемых ресурсов с Items (Максимум 1.0 - это 100%)", ref refundItemsPercent);
            GetVariable("Основные", "Процент возвращаемых ресурсов с построек (Максимум 1.0 - это 100%)", ref refundPercent);
            GetVariable("Основные", "Включить возрат объектов (При удаление объектов(сундуки, печки и тд.) будет возращать объект а не ресурсы)", ref refundItemsGive);
            GetVariable("Основные", "Процент выпадающих ресурсов (не вещей) с удаляемых ящиков (Максимум 1.0 - это 100%)", ref refundStoragePercent);
            GetVariable("Основные", "Разрешить удаление объектов друзей без авторизации в шкафу", ref friendRemove);
            GetVariable("Основные", "Разрешить удаление объектов соклановцев без авторизации в шкафу", ref clanRemove);
            GetVariable("Основные", "Разрешить удаление чужих объектов при наличии авторизации в шкафу", ref cupboardRemove);
            GetVariable("Основные", "Разрешить удаление собственных объектов без авторизации в шкафу", ref selfRemove);
            GetVariable("Основные", "Включить поддержку NoEscape (С сайта Oxide-Russia.ru)", ref useNoEscape);
            GetVariable("Основные", "Разрешить удаление обьектов друзьям", ref removeFriends);
            GetVariable("Основные", "Разрешить удаление объектов соклановцев", ref removeClans);
            GetVariable("Основные", "Разрешить удаление обьектов команде игрока (Team)", ref removeTeam);

            GetVariable("Основные", "Добавить автодобавление киянки в 7 слот игрока для включения ремува", ref EnabledHammer);

            GetVariable("GUI", "Панель AnchorMin", ref PanelAnchorMin);
            GetVariable("GUI", "Панель AnchorMax", ref PanelAnchorMax);
            GetVariable("GUI", "Цвет фона", ref PanelColor);
            GetVariable("GUI", "Размер текста", ref TextFontSize);
            GetVariable("GUI", "Цвет текста", ref TextСolor);
            GetVariable("GUI", "Текст AnchorMin", ref TextAnchorMin);
            GetVariable("GUI", "Текст AnchorMax", ref TextAnchorMax);
            GetVariable("Основные", "Привилегия игнорирования запрета удаления объектов какие были установлены N времени назад (Если включено)", ref IgnorePrivilage);
            var _BlackListed = new List<object>() {
                    {
                    "small_stash_deployed"
                }
                , {
                    "searchlight.deployed"
                }
            }
            ;
            GetVariable("Основные", "Список запрещенных для удаления Entity shortname (Не Item)", ref _BlackListed);
            BlackListed = _BlackListed.Select(p => p.ToString()).ToList();
            SaveConfig();
        }
        private bool GetVariable<T>(string MainMenu, string Key, ref T var)
        {
            if (Config[MainMenu, Key] != null)
            {
                var = (T)Convert.ChangeType(Config[MainMenu, Key], typeof(T));
                return false;
            }
            Config[MainMenu, Key] = var;
            return true;
        }
        static int constructionColl = LayerMask.GetMask(new string[] {
            "Construction", "Deployable", "Prevent Building", "Deployed"
        }
        );
        private static Dictionary<string, int> deployedToItem = new Dictionary<string, int>();
        Dictionary<BasePlayer, int> timers = new Dictionary<BasePlayer, int>();
        Dictionary<ulong, string> activePlayers = new Dictionary<ulong, string>();
        int currentRemove = 0;
        [PluginReference] Plugin Clans;
        [PluginReference] Plugin Friends;
        [PluginReference] Plugin NoEscape;
        [PluginReference] Plugin MutualPermission;
        [PluginReference] Plugin BuildingUpgrade;
        bool IsClanMember(ulong playerID, ulong targetID)
        {
            if (plugins.Exists("Clans"))
            {
                var result = Clans?.Call("IsTeammate", playerID, targetID) != null ? Clans?.Call("IsTeammate", playerID, targetID) : (bool)(Clans?.Call("HasFriend", playerID, targetID) ?? false);
                if (result != null)
                    return (bool)result;
            }
            return false;
        }
        bool IsFriends(ulong playerid = 1, ulong friendId = 0)
        {
            if (plugins.Exists("Friends"))
            {
                return (bool)(Friends?.Call("AreFriends", playerid, friendId) ?? false);
            }
            if (plugins.Exists("MutualPermission")) return (bool)(MutualPermission?.Call("isPermissionAllowed", "Home", playerid, friendId) ?? false);
            return false;
        }

        bool IsTeamate(BasePlayer player, ulong targetID)
        {
            if (player.currentTeam == 0) return false;
            var team = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
            if (team == null) return false;

            var list = RelationshipManager.ServerInstance.FindTeam(player.currentTeam).members.Where(p => p == targetID).ToList();
            return list.Count > 0;
        }

        private Dictionary<BasePlayer, DateTime> Cooldowns = new Dictionary<BasePlayer, DateTime>();
        private double Cooldown = 30f;

        private object OnActiveItemChange(BasePlayer player, Item oldItem, uint newItemId)
        {
            if (player == null || oldItem == null || oldItem.skin != HammerSkinID) return null;
            if (oldItem != null && oldItem.skin == HammerSkinID && activePlayers.ContainsKey(player.userID))
            {
                timers.Remove(player);
                DeactivateRemove(player.userID);
                DestroyUI(player);
            }
            return null;
        }

        object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null) return null;
            if (player.inventory.containerBelt.capacity > 6 && player.inventory.containerBelt.GetSlot(6) != null)
            {
                player.inventory.containerBelt.capacity = 6;
                player.inventory.containerBelt.GetSlot(6).RemoveFromContainer();
            }
            if (activePlayers.ContainsKey(player.userID))
            {
                timers.Remove(player);
                DeactivateRemove(player.userID);
                DestroyUI(player);
            }
            return null;
        }

        void OnItemDropped(Item item, BaseEntity entity)
        {
            if (item == null || !item.info.shortname.Contains("hammer") || item.skin != HammerSkinID) return;
            item.RemoveFromWorld();
        }

        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (player == null) return;
            if (EnTimedRemove)
            {
                if (newItem == null) return;
                if (newItem.info.shortname == "building.planner")
                {
                    if (Cooldowns.ContainsKey(player))
                    {
                        double seconds = Cooldowns[player].Subtract(DateTime.Now).TotalSeconds;
                        if (seconds >= 0) return;
                    }
                    //SendReply(player, Messages["enabledRemoveTimer"], NumericalFormatter.FormatTime(Timeout, player.userID));
                    Cooldowns[player] = DateTime.Now.AddSeconds(Cooldown);
                }
            }
            if (EnabledHammer && newItem != null && newItem.skin == HammerSkinID && !activePlayers.ContainsKey(player.userID))
            {
                if (EnabledBuildingUpgrade && BuildingUpgrade)
                {
                    var upgradeEnabled = (bool)BuildingUpgrade?.Call("BuildingUpgradeActivate", player.userID);
                    if (upgradeEnabled)
                        BuildingUpgrade?.Call("BuildingUpgradeDeactivate", player.userID);
                }
                var messages = EnabledHammer ? $"{ Messages["enabledRemove"]}\n{ Messages["EnabledHammer"]}" : Messages["enabledRemove"];
                SendReply(player, messages);
                timers[player] = resetTime;
                DrawUI(player, resetTime, "normal");
                ActivateRemove(player.userID, "normal");
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
            if (EnabledBuildingUpgrade && BuildingUpgrade)
            {
                if (BuildingUpgrade?.Call("BuildingUpgradeActivate", player.userID) != null)
                {
                    var upgradeEnabled = (bool)BuildingUpgrade?.Call("BuildingUpgradeActivate", player.userID);
                    if (upgradeEnabled)
                        BuildingUpgrade?.Call("BuildingUpgradeDeactivate", player.userID);
                }
            }

					if (useNoEscape)
                    {
                        if (plugins.Exists("NoEscape"))
                        {
                            var isRaid = (bool)NoEscape?.Call("IsRaidBlock", player.userID);
                            if (isRaid)
                            {
                                SendReply(player, "<color=#CF1E1E>•</color> Ошибка:\nВы не можете использовать ремув во время рейда");
                                return;
                            }
                        }
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
                    var messages = EnabledHammer ? $"{ Messages["enabledRemove"]}\n{ Messages["EnabledHammer"]}" : Messages["enabledRemove"];
                    SendReply(player, messages);
                    timers[player] = resetTime;
                    DrawUI(player, resetTime, "normal");
                    ActivateRemove(player.userID, "normal");
                    return;
                }
            }

            switch (args[0])
            {
                case "admin":
                    if (!permission.UserHasPermission(player.UserIDString, "remove.admin") && !player.IsAdmin)
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
                    timers[player] = resetTime;
                    DrawUI(player, resetTime, "admin");
                    ActivateRemove(player.userID, "admin");
                    break;
                case "all":
                    if (!permission.UserHasPermission(player.UserIDString, "remove.admin") && !player.IsAdmin)
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
                    timers[player] = resetTime;
                    DrawUI(player, resetTime, "all");
                    ActivateRemove(player.userID, "all");
                    break;
            }
        }

        void GiveHammer(BasePlayer player)
        {
            if (player.inventory.containerBelt.GetSlot(6) == null)
            {
                var item = ItemManager.CreateByName("hammer", 1, HammerSkinID);
                player.inventory.containerBelt.capacity = 7;
                item.MoveToContainer(player.inventory.containerBelt, 6);
            }
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;
            NextTick(() =>
            {
                GiveHammer(player);
            });
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
            if (EnabledBuildingUpgrade && BuildingUpgrade)
            {
                var upgradeEnabled = (bool)BuildingUpgrade?.Call("BuildingUpgradeActivate", player.userID);
                if (upgradeEnabled)
                    BuildingUpgrade?.Call("BuildingUpgradeDeactivate", player.userID);
            }
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
                    var messages = EnabledHammer ? $"{ Messages["enabledRemove"]}\n{ Messages["EnabledHammer"]}" : Messages["enabledRemove"];
                    SendReply(player, messages);
                    timers[player] = resetTime;
                    DrawUI(player, resetTime, "normal");
                    ActivateRemove(player.userID, "normal");
                    return;
                }
            }
            switch (args.Args[0])
            {
                case "admin":
                    if (!permission.UserHasPermission(player.UserIDString, "remove.admin") && !player.IsAdmin)
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
                    timers[player] = resetTime;
                    DrawUI(player, resetTime, "admin");
                    ActivateRemove(player.userID, "admin");
                    break;
                case "all":
                    if (!permission.UserHasPermission(player.UserIDString, "remove.admin") && !player.IsAdmin)
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
                    timers[player] = resetTime;
                    DrawUI(player, resetTime, "all");
                    ActivateRemove(player.userID, "all");
                    break;
            }
        }

        Dictionary<uint, float> entityes = new Dictionary<uint, float>();

        void LoadEntity()
        {
            try
            {
                entityes = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<uint, float>>($"Remove_NewEntity");
            }
            catch
            {
                entityes = new Dictionary<uint, float>();
            }
        }
        void CheckEntity()
        {
            if (entityes == null)
                entityes = new Dictionary<uint, float>();
            var entityList = entityes.Keys.ToList();
            for (int i = 0; i < entityList.Count; i++)
            {
                var Entity = BaseEntity.serverEntities.Find(entityList[i]);
                if (Entity == null)
                    entityes.Remove(entityList[i]);
            }
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (plan == null || go == null) return;
            if (EnTimedRemove)
            {
                BaseEntity entity = go.ToBaseEntity();
                if (entity?.net?.ID == null) return;
                entityes.Add(entity.net.ID, Timeout);
            }
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null || entity?.net?.ID == null) return;
            try
            {
                if (entityes.ContainsKey(entity.net.ID)) entityes.Remove(entity.net.ID);
            }
            catch (NullReferenceException) { }
        }

        void OnNewSave()
        {
            if (EnTimedRemove)
            {
                Puts("Обнаружен вайп. Очищаем сохраненные объекты");
                LoadEntity();
                entityes.Clear();
            }
        }

        void OnServerSave()
        {
            if (entityes != null)
                Interface.Oxide.DataFileSystem.WriteObject("Remove_NewEntity", entityes);
        }

        void Loaded()
        {
            inst = this;
            LoadDefaultConfig();
            if (EnTimedRemove)
            {
                LoadEntity();
                CheckEntity();
                Subscribe("OnEntityBuilt");
                Subscribe("OnEntityKill");
            }
            else
            {
                Unsubscribe("OnEntityBuilt");
                Unsubscribe("OnEntityKill");
                Unsubscribe("OnServerSave");
            }
            if (!EnabledHammer)
                Unsubscribe("OnPlayerRespawned");
            else
                Subscribe("OnPlayerRespawned");
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            PermissionService.RegisterPermissions(this, permisions);
            PermissionService.RegisterPermissions(this, new List<string>() {
                IgnorePrivilage
            }
            );
        }
        public List<string> permisions = new List<string>() {
            "remove.admin", "remove.use"};
        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.inventory.containerBelt.capacity > 6 && player.inventory.containerBelt.GetSlot(6) != null)
                {
                    player.inventory.containerBelt.capacity = 6;
                    player.inventory.containerBelt.GetSlot(6).RemoveFromContainer();
                }

                DestroyUI(player);

            }
            OnServerSave();
        }
        int check = 30;
        void OnServerInitialized()
        {
            if (removeFriends)
            {
                if (!plugins.Exists("Friends"))
                {
                    PrintWarning("Plugin Friends not found. Remove of friends buildings is disabled");
                    removeFriends = false;
                }
            }
            if (removeClans)
            {
                if (!plugins.Exists("Clans"))
                {
                    PrintWarning("Plugin Clans not found. Remove of clans buildings is disabled");
                    removeClans = false;
                }
            }
            InitRefundItems();

            List<ItemDefinition> ItemsDefinition = ItemManager.GetItemDefinitions() as List<ItemDefinition>;
            foreach (ItemDefinition itemdef in ItemsDefinition)
            {
                if (itemdef?.GetComponent<ItemModDeployable>() == null) continue;
                if (deployedToItem.ContainsKey(itemdef.GetComponent<ItemModDeployable>().entityPrefab.resourcePath)) continue;
                deployedToItem.Add(itemdef.GetComponent<ItemModDeployable>().entityPrefab.resourcePath, itemdef.itemid);
            }

            timer.Every(1f, TimerHandler);
            if (EnTimedRemove) timer.Every(check, TimerEntity);

            if (EnabledHammer)
                foreach (var player in BasePlayer.activePlayerList)
                {
                    GiveHammer(player);
                }
        }
        private bool CupboardPrivlidge(BasePlayer player, Vector3 position, BaseEntity entity)
        {
            return player.IsBuildingAuthed(position, new Quaternion(0, 0, 0, 0), new Bounds(Vector3.zero, Vector3.zero));
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
        bool OnRemoveActivate(ulong player)
        {
            if (activePlayers.ContainsKey(player))
            {
                return true;
            }
            return false;
        }
		
        void RemoveDeativate(ulong player)
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
		
        object OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (info == null) return null;
            var entity = info?.HitEntity;
            if (entity == null) return null;
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
                    if (!(entity is DecayEntity) && !deployedToItem.ContainsKey(entity.PrefabName)) return null;
                    if (!entity.OwnerID.IsSteamId()) return null;
                    StorageContainer storage = entity as StorageContainer;
                    if (storage && CheckRemoveItems)
                    {
                        if (storage.inventory.itemList.Count > 0)
                        {
                            SendReply(player, Messages["CheckItems"]);
                            return false;
                        }
                    }
                    var externalPlugins = Interface.CallHook("canRemove", player);
                    if (externalPlugins != null)
                    {
                        SendReply(player, Messages["RET"]);
                        return false;
                    }
                    if (useNoEscape)
                    {
                        if (plugins.Exists("NoEscape"))
                        {
                            var isRaid = (bool)NoEscape?.Call("IsRaidBlock", player.userID);
                            if (isRaid)
                            {
                                SendReply(player, Messages["NoEscape"]);
                                return false;
                            }
                        }
                    }
                    if (BlackListed.Contains(entity.ShortPrefabName))
                    {
                        SendReply(player, Messages["EntityBlackListed"]);
                        return false;
                    }
                    var privilege = player.GetBuildingPrivilege(player.WorldSpaceBounds());
                    if (cupboardRemove)
                    {
                        if (privilege != null && player.IsBuildingAuthed())
                        {
                            RemoveEntity(player, entity);
                            return true;
                        }
                    }
                    if (privilege != null && !player.IsBuildingAuthed())
                    {
                        if (selfRemove && entity.OwnerID == player.userID)
                        {
                            RemoveEntity(player, entity);
                            return true;
                        }
                        if (removeTeam)
                        {
                            if (IsTeamate(player, entity.OwnerID))
                            {
                                RemoveEntity(player, entity);
                                return true;
                            }
                        }
                        if (friendRemove)
                        {
                            if (removeFriends)
                            {
                                if (IsFriends(entity.OwnerID, player.userID))
                                {
                                    RemoveEntity(player, entity);
                                    return true;
                                }
                            }
                        }
                        if (clanRemove)
                        {
                            if (removeClans)
                            {
                                if (IsClanMember(entity.OwnerID, player.userID))
                                {
                                    RemoveEntity(player, entity);
                                    return true;
                                }
                            }
                        }
                        SendReply(player, Messages["ownerCup"]);
                        return false;
                    }
                    if (entity.OwnerID != player.userID)
                    {
                        if (removeTeam)
                        {
                            if (IsTeamate(player, entity.OwnerID))
                            {
                                RemoveEntity(player, entity);
                                return true;
                            }
                        }
                        if (removeFriends)
                        {
                            if (IsFriends(entity.OwnerID, player.userID))
                            {
                                RemoveEntity(player, entity);
                                return true;
                            }
                        }
                        if (removeClans)
                        {
                            if (IsClanMember(entity.OwnerID, player.userID))
                            {
                                RemoveEntity(player, entity);
                                return true;
                            }
                        }
                        SendReply(player, Messages["norights"]);
                        return false;
                    }
                    RemoveEntity(player, entity);
                    return true;
            }
            return null;
        }
		
        private static string Format(int units, string form1, string form2, string form3)
        {
            var tmp = units % 10;
            if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9) return $"{units} {form1}";
            if (tmp >= 2 && tmp <= 4) return $"{units} {form2}";
            return $"{units} {form3}";
        }
		
        private static class NumericalFormatter
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
                        case 1:
                            result = forms[0];
                            break;
                        case 2:
                        case 3:
                        case 4:
                            result = forms[1];
                            break;
                        default:
                            result = forms[2];
                            break;
                    }
                }
                return string.Format("{0} {1} ", origNum, result);
            }
            private static bool IsEng(object player) => inst.lang.GetLanguage(GetUserId(player)) != "ru⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠";
            private static string FormatSeconds(int seconds, bool eng) => GetNumEndings(seconds, eng ? new[] {
                "second", "seconds", "seconds"
            }
            : new[] {
                "секунда", "секунды", "секунд"
            }
            );
            private static string FormatMinutes(int minutes, bool eng) => GetNumEndings(minutes, eng ? new[] {
                "minute", "minutes", "minutes"
            }
            : new[] {
                "минута", "минуты", "минут"
            }
            );
            private static string FormatHours(int hours, bool eng) => GetNumEndings(hours, eng ? new[] {
                "hour", "hours", "hours"
            }
            : new[] {
                "час", "часа", "часов"
            }
            );
            private static string FormatDays(int days, bool eng) => GetNumEndings(days, eng ? new[] {
                "day", "days", "days"
            }
            : new[] {
                "день", "дня", "дней"
            }
            );
            private static string FormatTime(TimeSpan timeSpan, bool eng)
            {
                string result = string.Empty;
                if (timeSpan.Days > 0) result += FormatDays(timeSpan.Days, eng);
                if (timeSpan.Hours > 0) result += FormatHours(timeSpan.Hours, eng);
                if (timeSpan.Minutes > 0) result += FormatMinutes(timeSpan.Minutes, eng);
                if (timeSpan.Seconds > 0) result += FormatSeconds(timeSpan.Seconds, eng).TrimEnd(' ');
                return result;
            }
            public static string FormatTime(int seconds, object player) => FormatTime(new TimeSpan(0, 0, seconds), false);
            public static string FormatTime(float seconds, object player) => FormatTime((int)Math.Round(seconds), player);
            public static string FormatTime(TimeSpan time, object player) => FormatTime((int)Math.Round(time.TotalSeconds), player);
            public static string FromatSlots(int slots, object player) => GetNumEndings(slots, IsEng(player) ? new[] {
                "slot", "slots", "slots"
            }
            : new[] {
                "слот", "слота", "слотов"
            }
            );
        }
		
        private static string GetUserId(object player)
        {
            var id = player is BasePlayer ? ((BasePlayer)player).UserIDString : player.ToString();
            if (inst.permission.UserIdValid(id)) return id;
            inst.PrintError($"Trying to get the player data with invalide UserID \"{player}\"!");
            return null;
        }
        void TimerEntity()
        {
            List<uint> remove = entityes.Keys.ToList().Where(ent => (entityes[ent] -= check) < 0).ToList();
            List<uint> Remove = new List<uint>();
            foreach (var entity in entityes)
            {
                var seconds = entity.Value;
                if (seconds < 0.0f)
                {
                    Remove.Add(entity.Key);
                    continue;
                }
                if (seconds > Timeout)
                {
                    entityes[entity.Key] = Timeout;
                    continue;
                }
            }
            foreach (var id in Remove)
            {
                entityes.Remove(id);
            }
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
            if (EnTimedRemove && !PermissionService.HasPermission(player, IgnorePrivilage))
            {
                if (!entityes.ContainsKey(entity.net.ID))
                {
                    SendReply(player, Messages["blockremovetime"], NumericalFormatter.FormatTime(Timeout, player.userID));
                    return;
                }
            }
            Refund(player, entity);
            entity.Kill();
            UpdateTimer(player, "normal");
        }
		
        void RemoveEntityAdmin(BasePlayer player, BaseEntity entity)
        {
            entity.Kill();
            UpdateTimerAdmin(player, "admin");
        }
		
        void RemoveEntityAll(BasePlayer player, BaseEntity entity, Vector3 pos)
        {
            removeFrom.Add(pos);
            DelayRemoveAll();
            UpdateTimerAll(player, "all");
        }
		
        Dictionary<uint, Dictionary<ItemDefinition, int>> refundItems = new Dictionary<uint, Dictionary<ItemDefinition, int>>();
        void Refund(BasePlayer player, BaseEntity entity)
        {
            if (entity is BuildingBlock)
            {
                BuildingBlock buildingblock = entity as BuildingBlock;
                if (buildingblock.blockDefinition == null) return;
                int buildingblockGrade = (int)buildingblock.grade;
                if (buildingblock.blockDefinition.grades[buildingblockGrade] != null)
                {
                    float refundRate = buildingblock.healthFraction * refundPercent;
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
                        item.amount = (int)(item.amount * refundStoragePercent);
                        float single = 20f;
                        Vector3 vector32 = Quaternion.Euler(UnityEngine.Random.Range(-single * 0.1f, single * 0.1f), UnityEngine.Random.Range(-single * 0.1f, single * 0.1f), UnityEngine.Random.Range(-single * 0.1f, single * 0.1f)) * Vector3.up;
                        BaseEntity baseEntity = item.Drop(storage.transform.position + (Vector3.up * 0f), vector32 * UnityEngine.Random.Range(5f, 10f), UnityEngine.Random.rotation);
                        baseEntity.SetAngularVelocity(UnityEngine.Random.rotation.eulerAngles * 5f);
                    }
            }
            if (deployedToItem.ContainsKey(entity.gameObject.name))
            {
                ItemDefinition def = ItemManager.FindItemDefinition(deployedToItem[entity.gameObject.name]);
                foreach (var ingredient in def.Blueprint.ingredients)
                {
                    var reply = 3625;
                    if (reply == 0) { }
                    var amountOfIngridient = ingredient.amount;
                    var amount = Mathf.Floor(amountOfIngridient * refundItemsPercent);
                    if (amount <= 0 || amount > amountOfIngridient || amount >= int.MaxValue) amount = 1;
                    if (!refundItemsGive)
                    {
                        if (refundItemsPercent != 0)
                        {
                            Item x = ItemManager.Create(ingredient.itemDef, (int)amount);
                            player.GiveItem(x, BaseEntity.GiveItemReason.PickedUp);
                        }
                    }
                    else
                    {
                        GiveAndShowItem(player, deployedToItem[entity.PrefabName], 1);
                        return;
                    }
                }
            }
        }
		
        void GiveAndShowItem(BasePlayer player, int item, int amount)
        {
            Item x = ItemManager.CreateByItemID(item, amount);
            player.GiveItem(x, BaseEntity.GiveItemReason.PickedUp);
        }
		
        void InitRefundItems()
        {
            foreach (var item in ItemManager.itemList)
            {
                var deployable = item.GetComponent<ItemModDeployable>();
                if (deployable != null)
                {
                    if (item.Blueprint == null || deployable.entityPrefab == null) continue;
                    refundItems.Add(deployable.entityPrefab.resourceID, item.Blueprint.ingredients.ToDictionary(p => p.itemDef, p => (Mathf.CeilToInt(p.amount * refundPercent))));
                }
            }
        }
		
        private string GUI = @"[{""name"": ""remove.panel"",""parent"": ""Hud"",""components"": [{""type"": ""UnityEngine.UI.Image"",""color"": ""{PanelColor}""},{""type"": ""RectTransform"",""anchormin"": ""{PanelAnchorMin}"",""anchormax"": ""{PanelAnchorMax}""}]}, {""name"": ""remove.text"",""parent"": ""remove.panel"",""components"": [{""type"": ""UnityEngine.UI.Text"",""text"": ""{msg}"",""fontSize"": ""{TextFontSize}"",""align"": ""MiddleCenter""}, {""type"": ""UnityEngine.UI.Outline"",""color"": ""{TextСolor}"",""distance"": ""0.1 -0.1""}, {""type"": ""RectTransform"",""anchormin"": ""{TextAnchorMin}"",""anchormax"": ""{TextAnchorMax}""}]}]";
        void DrawUI(BasePlayer player, int seconds, string type)
        {
            DestroyUI(player);
            var msg = "";
            if (type == "normal")
            {
                msg = Messages["RNormal"];
            }
            else msg = type == "admin" ? Messages["RAdmin"] : Messages["RAll"];
            CuiHelper.AddUi(player, GUI.Replace("{PanelColor}", PanelColor.ToString()).Replace("{PanelAnchorMin}", PanelAnchorMin.ToString()).Replace("{PanelAnchorMax}", PanelAnchorMax.ToString()).Replace("{TextFontSize}", TextFontSize.ToString()).Replace("{TextСolor}", TextСolor.ToString()).Replace("{TextAnchorMin}", TextAnchorMin.ToString()).Replace("{TextAnchorMax}", TextAnchorMax.ToString()).Replace("{msg}", msg).Replace("{1}", NumericalFormatter.FormatTime(seconds, player.userID)));
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
            timers[player] = resetTime;
            DrawUI(player, timers[player], type);
        }
		
        void UpdateTimerAdmin(BasePlayer player, string type)
        {
            timers[player] = resetTime;
            DrawUI(player, timers[player], type);
        }
		
        void UpdateTimerAll(BasePlayer player, string type)
        {
            timers[player] = resetTime;
            DrawUI(player, timers[player], type);
        }
		
        Dictionary<string, string> Messages = new Dictionary<string, string>() {
                {
                "NoEscape", "Удаление построек во время рейдблока запрещёно!"
            }
            , {
                "blockremovetime", "Извините, но этот объект уже нельзя удалить, он был создан более чем <color=#ffd479>{0}</color> назад"
            }
            , {
                "NoPermission", "У Вас нету прав на использование этой команды"
            }
            , {
                "enabledRemove", "<size=16>Используйте киянку для удаления объектов</size>"
            }
            , {
                "enabledRemoveTimer", "<color=#ffd479>Внимание:</color> Объекты созданые более чем <color=#ffd479>{0}</color> назад, удалить нельзя"
            }
            , {
                "ownerCup", "Что бы удалять постройки, вы должны быть авторизированы в шкафу"
            }
            , {
                "norights", "Вы не имеете права удалять чужие постройки!"
            }
            , {
                "RNormal", "Режим удаления выключится через <color=#ffd479>{1}</color><size=10729></size>"
            }
            , {
                "RAdmin", "Режим админ удаления выключится через <color=#ffd479>{1}</color>"
            }
            , {
                "RAll", "Режим удаления всех объектов выключится через <color=#ffd479>{1}</color>"
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
                "EnabledHammer", "Нажмите <color=#ffd479>'7'</color> чтобы взять киянку для удаления предметов в руки"
            }
        }
        ;
        public static class PermissionService
        {
            public static Permission permission = Interface.GetMod().GetLibrary<Permission>();
            public static bool HasPermission(BasePlayer player, string permissionName)
            {
                if (player == null || string.IsNullOrEmpty(permissionName)) return false;
                var uid = player.UserIDString;
                if (permission.UserHasPermission(uid, permissionName)) return true;
                return false;
            }
            public static void RegisterPermissions(Plugin owner, List<string> permissions)
            {
                if (owner == null) throw new ArgumentNullException("owner⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠");
                if (permissions == null) throw new ArgumentNullException("commands⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠⁠");
                foreach (var permissionName in permissions.Where(permissionName => !permission.PermissionExists(permissionName)))
                {
                    permission.RegisterPermission(permissionName, owner);
                }
            }
        }
    }
}