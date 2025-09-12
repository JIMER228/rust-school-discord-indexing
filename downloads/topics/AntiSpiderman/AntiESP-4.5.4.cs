        #region Header
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using Network;
using Facepunch;
using Oxide.Core.Plugins;
using System;

namespace Oxide.Plugins {
    [Info("AntiESP", "https://topplugin.ru / https://discord.com/invite/5DPTsRmd3G", "4.5.4")]
    [Description("Предотвращает использование ESP скрывая объекты за стенами")]
    class AntiESP : RustPlugin {
        #endregion

        #region Fields
        [PluginReference]
        private Plugin Clans, Stealth;
        private static bool isStashesHideEnabled = true;
        private static AntiESP _instance;
        private static bool _loaded = false;
        private static float _collectRate = 2f;
        private static float _updateRate = 0.03f;
        private Dictionary<ulong, HashSet<ulong>> _playersClanMates = new Dictionary<ulong, HashSet<ulong>>();
        private HashSet<BaseNetworkable> _shelves = new HashSet<BaseNetworkable>();
        private static Dictionary<ulong, Handler> _handlers = new Dictionary<ulong, Handler>();
        private static int _layerConstruction = UnityEngine.LayerMask.GetMask("Construction");
        private static int _interestedLayers = LayerMask.GetMask("Deployed", "Player (Server)");
        private static HashSet<string> _whiteListEntities = new HashSet<string>() {
            "campfire",
            "box.wooden.large",
            "woodbox_deployed",
            "lantern.deployed",
            "cupboard.tool.deployed",
            "sleepingbag_leather_deployed",
            "bed_deployed",
            "shelves",
            "lock.code",
            "lock.key",
            "repairbench_deployed",
            "locker.deployed",
            "fridge.deployed",
            "ceilinglight.deployed",
            "tunalight.deployed",
        };
        #endregion

        [ConsoleCommand("antiesp.collectrate")]
        private void AntiESPCollectRateConsoleCommand(ConsoleSystem.Arg arg) {
            if (!arg.IsAdmin) return;
            float newValue;
            if (arg.Args == null || arg.Args.Length == 0) {
                SendReply(arg, $"Использование: antiesp.collectrate 'значение в пределах 2-5'");
                return;
            }
            if (!float.TryParse(arg.Args[0], out newValue)) {
                SendReply(arg, $"Использование: antiesp.collectrate 'значение в пределах 2-5'");
                return;
            }
            if (newValue < 2 || newValue > 5) {
                SendReply(arg, $"Значение должно быть > 2 и < 5");
                return;
            }
            var old = _collectRate;
            _collectRate = newValue;
            SendReply(arg, $"Старое значение CollectRate: {old}, новое значение: {_collectRate}");
        }

        [ConsoleCommand("antiesp.updaterate")]
        private void AntiESPUpdateRateConsoleCommand(ConsoleSystem.Arg arg) {
            if (!arg.IsAdmin) return;
            float newValue;
            if (arg.Args == null || arg.Args.Length == 0) {
                SendReply(arg, $"Использование: antiesp.updaterate 'значение в пределах 0.01-0.5'");
                return;
            }
            if (!float.TryParse(arg.Args[0], out newValue)) {
                SendReply(arg, $"Использование: antiesp.updaterate 'значение в пределах 0.01-0.5'");
                return;
            }
            if (newValue < 0.01f || newValue > 0.5f) {
                SendReply(arg, $"Значение должно быть > 0.01 и < 0.5");
                return;
            }
            var old = _updateRate;
            _updateRate = newValue;
            SendReply(arg, $"Старое значение UpdateRate: {old}, новое значение: {_updateRate}");
        }

        #region Behaviours

        private class StashHandler : MonoBehaviour {
            public StashContainer Stash;
            public float nextTimeSee;
            public Dictionary<ulong, float> timeClose = new Dictionary<ulong, float>();

            private void Awake() {
                Stash = GetComponent<StashContainer>();
                if (Stash.IsHidden())
                    Hide();
            }

            public void Hide() {
                if (Stash.limitNetworking) return;
                nextTimeSee = Time.realtimeSinceStartup + 3;
                Stash.DisableNetworking();
            }

            private void Update() {
                foreach (var player in BasePlayer.activePlayerList) {
                    if (Stash.limitNetworking) {
                        if (Stash.PlayerInRange(player)) {
                            if (nextTimeSee - Time.realtimeSinceStartup < 0) {
                                float time;
                                if (!timeClose.TryGetValue(player.userID, out time)) {
                                    timeClose[player.userID] = 0;
                                } else {
                                    timeClose[player.userID] += Time.deltaTime;
                                }
                                if (time > 1) {
                                    Show();
                                    timeClose.Remove(player.userID);
                                }
                            }
                        } else {
                            if (timeClose.ContainsKey(player.userID)) {
                                timeClose[player.userID] -= Time.deltaTime;
                                if (timeClose[player.userID] < 0) {
                                    timeClose.Remove(player.userID);
                                }
                            }
                        }
                    }
                }
            }

            private void Show() {
                Stash.SetFlag(BaseEntity.Flags.Disabled, false, false, true);
                Stash.limitNetworking = false;
                Stash.SetHidden(false);
            }

            public void Destroy() {
                Stash.SetFlag(BaseEntity.Flags.Disabled, false, false, true);
                Stash.limitNetworking = false;
                UnityEngine.GameObject.Destroy(this);
            }
        }

        class Handler : MonoBehaviour {
            public BasePlayer player;
            private List<BaseNetworkable> deleteShelves = new List<BaseNetworkable>();
            private Dictionary<uint, float> lastEntityWrite = new Dictionary<uint, float>();
            private HashSet<BaseEntity> entityList = new HashSet<BaseEntity>();
            private Vector3 playerPreviousPosition;
            private float lastTick = Time.realtimeSinceStartup;
            private float lastEntityCollect = Time.realtimeSinceStartup;
            private float lastSleepersCollect = Time.realtimeSinceStartup;
            private float lastClanMatesCollect;
            private float uniqUpdateRateModifier = UnityEngine.Random.Range(-(_collectRate / 20), _collectRate / 20);
            private int currentEntityPosition = 0;
            private bool isCollectingEntities = false;
            private bool isCollectingSleepers = false;

            private void Awake() {
                player = GetComponent<BasePlayer>();
                _handlers[player.userID] = this;
                GetClanMates();
            }

            private void LateUpdate() {
                if (player.IsSleeping()) return;
                if (player.transform.position == playerPreviousPosition || (player.transform.position - playerPreviousPosition).sqrMagnitude < 0.00001f)
                if (Time.realtimeSinceStartup - lastTick < _updateRate) return;
                lastTick = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - lastEntityCollect > UpdateRate) {
                    entityList.Clear();
                    GetEntities();
                    lastEntityCollect = Time.realtimeSinceStartup;
                }
                if (Time.realtimeSinceStartup - lastSleepersCollect > UpdateRate + 10) {
                    if (!isCollectingSleepers) {
                        StartCoroutine(CheckSleepers());
                        lastSleepersCollect = Time.realtimeSinceStartup;
                    }
                }
                if (entityList.Count == 0) return;
                if (entityList.Count - 1 < currentEntityPosition) {
                    currentEntityPosition = 0;
                    return;
                }
                var target = entityList.ElementAtOrDefault(currentEntityPosition);
                if (target == null || target.IsDestroyed) {
                    entityList.Remove(target);
                    goto NEXT;
                }
                if (NeedCheck(target.net.ID)) {
                    GetClanMates();
                    if (target.OwnerID == player.userID || IsClanMates(target.OwnerID) || player.IsAdmin)
                        Show(target);
                    else {
                        if (target is BasePlayer) {
                            if (TryLineCast(player.eyes.position, target.transform.position))
                                Show(target);
                        } else if (IsObjectVisible(target.WorldSpaceBounds().ToBounds().center))
                            Show(target);
                    }
                }
                playerPreviousPosition = player.transform.position;
                NEXT:
                if (entityList.Count >= currentEntityPosition + 1)
                    currentEntityPosition++;
                else
                    currentEntityPosition = 0;
            }

            private IEnumerator CheckSleepers() {
                isCollectingSleepers = true;
                for (int i = BasePlayer.sleepingPlayerList.Count - 1; i >= 0; i--) {
                    if (i > BasePlayer.sleepingPlayerList.Count - 1) break;
                    var sleeper = BasePlayer.sleepingPlayerList[i];
                    if (sleeper == null || sleeper.IsDestroyed) continue;
                    if (Vector3.Distance(sleeper.transform.position, player.transform.position) < 100)
                        if (!IsPlayerInvisible(sleeper.userID) && TryLineCast(player.eyes.position, sleeper.transform.position))
                            Show(sleeper);
                    if (i % 1 == 0)
                        yield return null;
                }
                isCollectingSleepers = false;
            }
            
            private void GetShelves() {
                foreach (var shelve in _instance._shelves) {
                    if (shelve == null || shelve.IsDestroyed) {
                        deleteShelves.Add(shelve);
                        continue;
                    }
                    if ((player.transform.position - shelve.transform.position).sqrMagnitude <= 50)
                        entityList.Add(shelve as BaseEntity);
                }
                _instance._shelves.ExceptWith(deleteShelves);
                deleteShelves.Clear();
            }

            private void GetEntities() {
                GetShelves();
                List<Collider> list2 = Pool.GetList<Collider>();
                Vis.Colliders(player.transform.position, 50, list2, _interestedLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < list2.Count; i++) {
                    var e = list2[i].gameObject.ToBaseEntity();
                    if ((e != null && !e.IsDestroyed) && IsInterestedEntity(e))
                        entityList.Add(e);
                }
                Pool.FreeList(ref list2);
            }

            private void GetClanMates() {
                if (Time.realtimeSinceStartup - lastClanMatesCollect > UpdateRate + 180) {
                    lastClanMatesCollect = Time.realtimeSinceStartup;
                    var list = new List<ulong>();
                    var teamMates = GetTeamMates();
                    var clanMembers = GetClanMembers();
                    if (teamMates != null)
                        list.AddRange(teamMates);
                    if (clanMembers != null)
                        list.AddRange(clanMembers);
                    if (list.Count > 0) {
                        if (!_instance._playersClanMates.ContainsKey(player.userID))
                            _instance._playersClanMates[player.userID] = new HashSet<ulong>();
                        _instance._playersClanMates[player.userID].Clear();
                        _instance._playersClanMates[player.userID].UnionWith(list);
                    } else
                        _instance._playersClanMates.Remove(player.userID);
                }
            }

            private List<ulong> GetClanMembers() {
                var clanMates = (List<ulong>)_instance.Clans?.Call("GetClanMembersByPlayer", player.userID);
                if (clanMates != null)
                    return clanMates;
                return null;
            }

            private List<ulong> GetTeamMates() {
                if (player.currentTeam != 0) {
                    var team = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                    if (team != null)
                        return team.members;
                }
                return null;
            }

            private bool IsClanMates(ulong targetID) {
                HashSet<ulong> list;
                if (_instance._playersClanMates.TryGetValue(player.userID, out list)) {
                    return list.Contains(targetID);
                }
                return false;
            }

            private bool NeedCheck(uint entityID) {
                float value;
                if (lastEntityWrite.TryGetValue(entityID, out value))
                    if (Time.realtimeSinceStartup - value < 20) return false;
                return true;
            }

            private bool TryLineCast(Vector3 start, Vector3 target, float plusPlayer = 0) {
                start.y += plusPlayer;
                return !Physics.Linecast(start, target, _layerConstruction);
            }

            private bool IsObjectVisible(Vector3 target) {
                if (TryLineCast(player.eyes.position, target)) return true;
                if (Vector3.Distance(player.transform.position, target) > 25) return false;
                if (TryLineCast(player.transform.position, target, 0.5f)) return true;
                if (TryLineCast(player.transform.position, target, 0.25f)) return true;
                if (TryLineCast(player.transform.position, target, 1f)) return true;
                return false;
            }

            public void Show(BaseEntity entity) {
                if (!_loaded) return;
                _instance.Write(player, entity);
                entityList.Remove(entity);
                lastEntityWrite[entity.net.ID] = Time.realtimeSinceStartup;
                foreach (var e in entity.children)
                    _instance.Write(player, e);
            }

            private float UpdateRate => uniqUpdateRateModifier + _collectRate;

            public void Destroy(bool remove = true) {
                if (remove)
                    _handlers.Remove(player.userID);
                GameObject.Destroy(this);
            }
        }
        #endregion

        #region Helpers

        private static bool IsPlayerInvisible(ulong userID) {
            var isPlayerInvisible = _instance.Stealth?.Call("IsPlayerInvisible", userID);
            if (isPlayerInvisible == null) return false;
            return (bool)isPlayerInvisible;
        }

        private void OnClanRemoveMember(string clanName, ulong userID) {
            _playersClanMates.Remove(userID);
            CommunityEntity.ServerInstance.StartCoroutine(RemoveFromClanMatesCoroutine(userID));
        }

        private IEnumerator RemoveFromClanMatesCoroutine(ulong userID) {
            foreach (var list in _playersClanMates.Values.ToList()) {
                for (int i = list.Count - 1; i >= 0; i--) {
                    if (list.ElementAt(i) == userID)
                        list.Remove(userID);
                    yield return null;
                }
            }
        }

        private void WriteForAll(BasePlayer player, BaseNetworkable entity, bool shoudInvalidate = false) {
            if (shoudInvalidate && (player == null || player.net.connection == null || entity == null || entity.IsDestroyed)) return;
            Write(player, entity, !shoudInvalidate);
            WriteForMates(player, entity, !shoudInvalidate);
        }

        private void WriteForMates(BasePlayer player, BaseNetworkable entity, bool shoudInvalidate = false) {
            HashSet<ulong> list;
            if (_playersClanMates.TryGetValue(player.userID, out list)) {
                foreach (var mate in list) {
                    var playerMate = BasePlayer.FindByID(mate);
                    if (playerMate != null && playerMate.IsConnected && Vector3.Distance(entity.transform.position, playerMate.transform.position) < 100)
                        Write(playerMate, entity, shoudInvalidate);
                }
            }
        }

        private void Write(BasePlayer player, BaseNetworkable entity, bool shoudInvalidate = false) {
            if (shoudInvalidate && (player == null || player.net.connection == null || entity == null || entity.IsDestroyed)) return;
                Connection conn = player.net.connection;
                conn.validate.entityUpdates = conn.validate.entityUpdates + 1u;
                BaseNetworkable.SaveInfo saveInfo = new BaseNetworkable.SaveInfo {
                    forConnection = player.net.connection,
                    forDisk = false
                };
                NetWrite write = Net.sv.StartWrite();
                write.PacketID(Message.Type.Entities);
                write.UInt32(player.net.connection.validate.entityUpdates);
                entity.ToStreamForNetwork(Net.sv.write, saveInfo);
                write.Send(new SendInfo(player.net.connection));
            }
        }

        private static bool IsInterestedEntity(BaseNetworkable entity) {
            if (entity is BaseLock) {
                var parent = (entity as BaseEntity).GetParentEntity();
                if (parent != null && !(parent is StorageContainer)) return false;
            }
            if ( _whiteListEntities.Contains(entity.ShortPrefabName)) return true;
            var bp = entity as BasePlayer;
            if (bp != null && !bp.IsConnected && !IsPlayerInvisible(bp.userID)) return true;
            return false;
        }

        private IEnumerator HideAllEntities(bool hide) {
            string operation = hide ? "скрытие" : "показ";
            PrintWarning($"Начинаем {operation} объектов...");
            int i = 0;
            int all = 0;
            foreach (var entity in BaseNetworkable.serverEntities.ToList()) {
                if (entity == null || entity.IsDestroyed) continue;

                var npc = entity as BasePlayer;
                if (npc != null && !npc.userID.IsSteamId()) {
                    continue;
                }

                if (isStashesHideEnabled && hide) {
                    if (entity is StashContainer) {
                        GetStashHandler(entity as BaseEntity);
                        continue;
                    }
                }
                if (hide == true && entity is Door)
                    foreach (var e in entity.children)
                        e.limitNetworking = false;
                if (IsInterestedEntity(entity)) {
                    i++;
                    if (i % 1000 == 0)
                        PrintWarning($"Обработано {i} объектов...");
                    entity.limitNetworking = hide;
                    var group = Net.sv.visibility.GetGroup(entity.transform.position);
                    entity.net.SwitchGroup(group);
                    if (hide && entity.ShortPrefabName == "shelves")
                        _shelves.Add(entity);
                }
                all++;
                if (all % 500 == 0)
                yield return null;
            }
            PrintWarning($"Обработка {i} объектов завершена.");
            if (hide)
                _loaded = true;
        }

        private StashHandler GetStashHandler(BaseEntity entity) {
            var handler = entity.GetComponent<StashHandler>() ?? entity.gameObject.AddComponent<StashHandler>();
            return handler;
        }

        #endregion

        #region Oxide

        private void Loaded() {
            _instance = this;
            if (!isStashesHideEnabled) {
                Unsubscribe("CanHideStash");
            }
        }

        private void CanHideStash(BasePlayer player, StashContainer stash) {
            timer.Once(1, () => {
                if (stash != null && !stash.IsDestroyed)
                    GetStashHandler(stash).Hide();
            });
        }

        private void OnServerInitialized() {
            CommunityEntity.ServerInstance.StartCoroutine(HideAllEntities(true));
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        private void OnEntitySpawned(BaseNetworkable entity) {
            if (!IsInterestedEntity(entity)) return;
            var npc = entity as BasePlayer;
            if (npc != null && !npc.userID.IsSteamId()) {
                return;
            }
            entity.limitNetworking = true;
            var group = Net.sv.visibility.GetGroup(entity.transform.position);
            entity.net.SwitchGroup(group);
            var baseEntity = entity as BaseEntity;
            if (baseEntity.OwnerID == 0) return;
            if (entity.ShortPrefabName == "shelves")
                _shelves.Add(entity);
            var player = BasePlayer.FindByID(baseEntity.OwnerID);
            player.GetComponent<Handler>()?.Show(baseEntity);
            WriteForMates(player, baseEntity, true);
        }

        private void OnEntityTakeDamage(BaseNetworkable entity, HitInfo info) {
            if (entity == null || entity.IsDestroyed) return;
            var player = info.InitiatorPlayer;
            if (player == null) return;
            if (!IsInterestedEntity(entity)) return;
            NextTick(() => WriteForAll(player, entity, true));
        }

        private void OnPlayerConnected(BasePlayer player) {
            if (!IsPlayerInvisible(player.userID))
                player.limitNetworking = false;
            if (player.GetComponent<Handler>() != null)
                GameObject.Destroy(player.GetComponent<Handler>());
            player.gameObject.AddComponent<Handler>();
        }

        private void OnPlayerDisconnected(BasePlayer player) {
            player.GetComponent<Handler>().Destroy();
            player.limitNetworking = true;
        }

        private void Unload() {
            foreach (var player in BasePlayer.activePlayerList)
                player.GetComponent<Handler>()?.Destroy(false);
            CommunityEntity.ServerInstance.StartCoroutine(HideAllEntities(false));
            if (isStashesHideEnabled) {
                var stashes = GameObject.FindObjectsOfType<StashContainer>();
                foreach (var s in stashes) {
                    s.GetComponent<StashHandler>()?.Destroy();
                }
            }
        }

        private void OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player) => NextTick(() => WriteForAll(player, privilege, true));

        private void OnCupboardDeauthorize(BuildingPrivlidge privilege, BasePlayer player) => NextTick(() => WriteForAll(player, privilege, true));

        private void OnCupboardClearList(BuildingPrivlidge privilege, BasePlayer player) => NextTick(() => WriteForAll(player, privilege, true));

        private void OnCodeEntered(CodeLock codeLock, BasePlayer player, string code) => NextTick(() => WriteForAll(player, codeLock, true));

        private void OnOvenToggle(BaseOven oven, BasePlayer player) => NextTick(() => WriteForAll(player, oven, true));

        private void CanLock(BasePlayer player, BaseLock baseLock) => NextTick(() => WriteForAll(player, baseLock, true));

        private void CanUnlock(BasePlayer player, BaseLock baseLock) => NextTick(() => WriteForAll(player, baseLock, true));

        private void CanChangeCode(BasePlayer player, CodeLock codeLock, string newCode, bool isGuestCode) => NextTick(() => WriteForAll(player, codeLock, true));
        
        #endregion

        #region Footer
    }
}
#endregion