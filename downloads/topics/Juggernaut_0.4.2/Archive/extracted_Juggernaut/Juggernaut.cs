using Facepunch;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Ext.Chaos;
using Oxide.Ext.Chaos.Data;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using UnityEngine;
using System.Linq;
using System.Globalization;

namespace Oxide.Plugins
{
    [Info("Juggernaut", "k1lly0u", "0.4.2")]
    [Description("A minigame where 1 player becomes a juggernaut and must make it to a set destination in order to win")]
    class Juggernaut : ChaosPlugin
    {
        #region Fields
        [PluginReference] Plugin EventManager;

        private static Datafile<Restoration> restoreData;

        private static Datafile<PrizeData> prizeData;


        private BasePlayer juggernaut = null;

        private FinishZone finishZone = null;

        private RoadFlare signalFlare = null;


        private Timer eventTimer;

        private double timerExpire;


        private List<ulong> contestants = Pool.Get<List<ulong>>();

        private List<ulong> juggernautTeamMembers = Pool.Get<List<ulong>>();

        private List<MapMarkerGenericRadius> markers = Pool.Get<List<MapMarkerGenericRadius>>();

        private List<KeyValuePair<Vector3, Vector3>> destinations = new List<KeyValuePair<Vector3, Vector3>>();

               
        private static Juggernaut Instance { get; set; }

        private static bool IsUnloading { get; set; } = false;


        private const string MARKER_PREFAB = "assets/prefabs/tools/map/genericradiusmarker.prefab";

        private const string FLARE_PREFAB = "assets/prefabs/tools/flareold/flare.deployed.prefab";

        private const string JUGGERNAUT_PERMISSION = "juggernaut.canenter";

        private const string UI_PANEL = "juggernaut.ui";

        private const string UI_NOTIFICATION = "juggernaut.notification";

        private const int TARGET_LAYERS = ~(1 << 10 | 1 << 18 | 1 << 28 | 1 << 29);


        private EventStatus status = EventStatus.Finished;

        private enum EventStatus { Finished, Open, Started }
        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            Instance = this;
            IsUnloading = false;

            permission.RegisterPermission(JUGGERNAUT_PERMISSION, this);

            lang.RegisterMessages(Messages, this);

            UnsubscribeHooks();

            restoreData = new Datafile<Restoration>("juggernaut_restore_data");
            restoreData.Data.SetFlags(Restoration.Flags.RestoreHealth | Restoration.Flags.RestoreInventory | Restoration.Flags.RestoreMetabolism | Restoration.Flags.RestorePosition | Restoration.Flags.RestoreTeam); //709
            prizeData = new Datafile<PrizeData>("juggernaut_winnings_data");
        }

        private void OnServerInitialized()
        {
            GenerateDestinations();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);

            TimedEventStart();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsSleeping() || player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.In(1, () => OnPlayerConnected(player));
                return;
            }

            if (restoreData.Data.HasData(player.userID))
                restoreData.Data.Restore(player);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (status == EventStatus.Open && contestants.Contains(player.userID.Get()))
                contestants.Remove(player.userID.Get());

            if (status == EventStatus.Started && player == juggernaut)
            {
                BroadcastAll("Notification.Disconnected");
                EndEvent();
            }
        }

        private object OnMapMarkerAdd(BasePlayer player, MapNote mapNote) => MapMarkerInternal(player);

        private object OnMapMarkerRemove(BasePlayer player, List<MapNote> mapNotes, int index) => MapMarkerInternal(player);

        private object OnMapMarkersClear(BasePlayer player, List<MapNote> mapNotes) => MapMarkerInternal(player);

        private object MapMarkerInternal(BasePlayer player)
        {
            if (player == juggernaut)
                return true;
            return null;
        }

        private object OnTeamLeave(RelationshipManager.PlayerTeam playerTeam, BasePlayer player) => OnTeamInternal(player);  
        
        private object OnTeamCreate(BasePlayer player) => OnTeamInternal(player);

        private object OnTeamInvite(BasePlayer player, BasePlayer other)
        {
            if (player == juggernaut || other == juggernaut)
                return true;
            return null;
        }

        private object OnTeamInternal(BasePlayer player)
        {
            if (player == juggernaut)
                return true;
            return null;
        }

        private void OnTeamUpdated(ulong teamId, ProtoBuf.PlayerTeam playerTeam, BasePlayer player)
        {
            if (player == juggernaut)
                SendMarkerUpdate("Destination");
        }

        private object OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            if (!player || player.IsAdmin || args == null)
                return null;

            if (player == juggernaut)
            {
                if (Configuration.Game.CommandBlacklist.Any(x => x.StartsWith("/") ? x.Substring(1).ToLower() == command : x.ToLower() == command))
                {
                    SendReply(player, msg("Notification.BlacklistedCommand", player.userID));
                    return false;
                }
            }
            return null;
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (!player || player.IsAdmin || arg.Args == null)
                return null;

            if (player == juggernaut)
            {
                if (Configuration.Game.CommandBlacklist.Any(x => arg.cmd.FullName.Equals(x.ToLower())))
                {
                    SendReply(player, msg("Notification.BlacklistedCommand", player.userID));
                    return false;
                }
            }
            return null;
        }

        private object CanLootPlayer(BasePlayer target, BasePlayer looter) => CanLootInternal(looter);

        private object CanLootEntity(BasePlayer player, BaseEntity entity) => CanLootInternal(player);

        private object OnItemPickup(Item item, BasePlayer player) => CanLootInternal(player);

        private object OnCollectiblePickup(Item item, BasePlayer player) => CanLootInternal(player);

        private object OnGrowableGather(GrowableEntity plant, Item item, BasePlayer player) => CanLootInternal(player);

        private object CanTakeCutting(BasePlayer player, GrowableEntity entity) => CanLootInternal(player);
        
        private object CanLootInternal(BasePlayer player)
        {            
            if (player == juggernaut && !Configuration.Juggernaut.CanLoot)
                return true;
            return null;
        }

        private object CanUseVending(VendingMachine machine, BasePlayer player)
        {
            if (player == juggernaut && !Configuration.Juggernaut.CanLoot)
                return false;
            return null;
        }

        private object CanMountEntity(BasePlayer player, BaseMountable mountable)
        {
            if (player == juggernaut && Configuration.Juggernaut.DisableMounting)
                return true;
            return null;
        }

        private void OnEntityTakeDamage(BaseCombatEntity baseCombatEntity, HitInfo hitInfo)
        {
            if (!baseCombatEntity || hitInfo == null)
                return;

            BasePlayer victim = baseCombatEntity.ToPlayer();
            BasePlayer attacker = hitInfo.InitiatorPlayer;

            if (victim == juggernaut)
            {
                if (hitInfo.Initiator && hitInfo.Initiator.ShortPrefabName.Equals("beartrap") && Configuration.Juggernaut.NoBeartrapDamage)
                {
                    hitInfo.damageTypes.ScaleAll(0);
                    return;
                }

                if (hitInfo.WeaponPrefab && hitInfo.WeaponPrefab.ShortPrefabName.Equals("landmine") && Configuration.Juggernaut.NoLandmineDamage)
                {
                    hitInfo.damageTypes.ScaleAll(0);
                    return;
                }

                if (hitInfo.damageTypes.GetMajorityDamageType() == Rust.DamageType.Fall && Configuration.Juggernaut.NoFallDamage)
                {
                    hitInfo.damageTypes.ScaleAll(0);
                    return;
                }

                if (attacker)
                {
                    if (juggernautTeamMembers.Contains(attacker.userID.Get()))
                    {
                        hitInfo.damageTypes.ScaleAll(0);
                        SendReply(attacker, msg("Notification.FriendlyFire", attacker.userID));
                        return;
                    }                   
                }

                hitInfo.damageTypes.ScaleAll(Configuration.Juggernaut.DefenseModifier);
                return;
            }

            if (attacker == juggernaut)
            {
                if (!Configuration.Juggernaut.DamageStructures && baseCombatEntity is BuildingBlock or SimpleBuildingBlock)
                {
                    hitInfo.damageTypes.ScaleAll(0);
                    return;
                }

                hitInfo.damageTypes.ScaleAll(Configuration.Juggernaut.AttackModifier);
            }
        }

        private object CanEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            BasePlayer target = entity as BasePlayer;
            if (target)
            {
                if (target == juggernaut)
                    return true;

                if (info.InitiatorPlayer && info.InitiatorPlayer == juggernaut)
                    return true;                
            }
            return null;
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo hitInfo)
        {
            if (!player || hitInfo == null)
                return;

            if (player != juggernaut)
                return;
            
            ClearJuggernautMarkers();
            
            bool isValid = false;

            BasePlayer attacker = hitInfo.InitiatorPlayer;
            if (attacker)
            {
                if (attacker == player)
                    BroadcastAll("Notification.JuggernautSuicide");
                else
                {
                    BroadcastAll("Notification.JuggernautKilledBy", attacker.displayName);
                    isValid = true;

                    if (Configuration.Prizes.Amount > 0)
                    {
                        if (Configuration.Prizes.Economics && Economics.IsLoaded)
                            Economics.Deposit(attacker.userID, (double)Configuration.Prizes.Amount);
                        if (Configuration.Prizes.ServerRewards && ServerRewards.IsLoaded)
                            ServerRewards.AddPoints(attacker.userID, Configuration.Prizes.Amount);
                    }
                }
            }
            else
            {
                BroadcastAll("Notification.JuggernautDead");
            }
            
            if (!isValid || !Configuration.Prizes.Inventory || Configuration.Prizes.Loot.Enabled)
                player.StripInventory();

            if (Configuration.Prizes.Loot.Enabled)
                Configuration.Prizes.Loot.PopulateLoot(player);

            UnlockInventory(player);
        }

        private void OnEntitySpawned(LootableCorpse corpse)
        {
            if (!corpse)
                return;

            if (corpse.playerSteamID != juggernaut.userID) 
                return;
            
            if (Configuration.Prizes.Inventory && Configuration.Prizes.RemoveAttire)
            {
                for (int i = corpse.containers[1].itemList.Count - 1; i >= 0; i--)
                {
                    Item item = corpse.containers[1].itemList[i];
                    if (item == null)
                        continue;
                        
                    item.RemoveFromContainer();
                    item.Remove();
                }
            }
                                
            OnEventLose(false);
        }

        private object CanNetworkTo(RoadFlare entity, BasePlayer target)
        {
            if (!entity || !target || !signalFlare || !juggernaut)
                return null;

            if (entity == signalFlare && target != juggernaut)
                return false;

            return null;
        }

        private object CanBeTargeted(BaseCombatEntity entity, HelicopterTurret turret)
        {
            if (entity == juggernaut)
                return false;

            return null;
        }

        private object CanHelicopterTarget(PatrolHelicopterAI heli, BasePlayer player)
        {
            if (player == juggernaut)
                return false;

            return null;
        }
        private object OnHelicopterTarget(HelicopterTurret turret, BaseCombatEntity entity)
        {
            if (entity == juggernaut)
                return false;

            return null;
        }

        private void Unload()
        {           
            IsUnloading = true;

            if (status == EventStatus.Started)
                EndEvent();

            DestroyUI();
            
            LootHandler[] lootHandlers = UnityEngine.Object.FindObjectsOfType<LootHandler>();
            for (int i = 0; i < lootHandlers.Length; i++)
            {
                LootHandler lootHandler = lootHandlers[i];
                if (lootHandler.Looter)                
                    lootHandler.Looter.EndLooting();                
                UnityEngine.Object.Destroy(lootHandler);
            }

            Pool.FreeUnmanaged(ref contestants);
            Pool.FreeUnmanaged(ref juggernautTeamMembers);
            Pool.FreeUnmanaged(ref markers);

            restoreData = null;
            prizeData = null;
            Instance = null;
        }
        #endregion

        #region Event Management
        private void UnsubscribeHooks()
        {
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnPlayerDeath));
            Unsubscribe(nameof(OnMapMarkerAdd));
            Unsubscribe(nameof(OnMapMarkerRemove));
            Unsubscribe(nameof(OnMapMarkersClear));
            Unsubscribe(nameof(OnTeamCreate));
            Unsubscribe(nameof(OnTeamInvite));
            Unsubscribe(nameof(OnTeamLeave));
            Unsubscribe(nameof(OnTeamUpdated));
            Unsubscribe(nameof(OnPlayerCommand));
            Unsubscribe(nameof(OnServerCommand));
            Unsubscribe(nameof(CanMountEntity));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(CanLootEntity));
            Unsubscribe(nameof(CanLootEntity));
            Unsubscribe(nameof(OnItemPickup));
            Unsubscribe(nameof(OnCollectiblePickup));
            Unsubscribe(nameof(OnGrowableGather));
            Unsubscribe(nameof(CanTakeCutting));
            Unsubscribe(nameof(CanUseVending));
            Unsubscribe(nameof(CanNetworkTo));
            Unsubscribe(nameof(CanBeTargeted));
            Unsubscribe(nameof(CanHelicopterTarget));
            Unsubscribe(nameof(OnHelicopterTarget));
        }

        private void SubscribeHooks()
        {
            Subscribe(nameof(OnEntityTakeDamage));
            Subscribe(nameof(OnPlayerDeath));
            Subscribe(nameof(OnMapMarkerAdd));
            Subscribe(nameof(OnMapMarkerRemove));
            Subscribe(nameof(OnMapMarkersClear));
            Subscribe(nameof(OnTeamCreate));
            Subscribe(nameof(OnTeamInvite));
            Subscribe(nameof(OnTeamLeave));
            Subscribe(nameof(OnTeamUpdated));
            Subscribe(nameof(OnPlayerCommand));
            Subscribe(nameof(OnServerCommand));
            Subscribe(nameof(CanMountEntity));
            Subscribe(nameof(OnEntitySpawned));
            Subscribe(nameof(CanLootEntity));
            Subscribe(nameof(CanLootEntity));
            Subscribe(nameof(OnItemPickup));
            Subscribe(nameof(OnCollectiblePickup));
            Subscribe(nameof(OnGrowableGather));
            Subscribe(nameof(CanTakeCutting));
            Subscribe(nameof(CanUseVending));
            Subscribe(nameof(CanNetworkTo));
            Subscribe(nameof(CanBeTargeted));
            Subscribe(nameof(CanHelicopterTarget));
            Subscribe(nameof(OnHelicopterTarget));
        }

        private void TimedEventStart()
        {
            if (IsUnloading)
                return;

            status = EventStatus.Finished;

            if (Configuration.Timers.Interval < 1)
                return;

            eventTimer?.Destroy();
            eventTimer = timer.In(Configuration.Timers.Interval, () =>
            {
                if (BasePlayer.activePlayerList.Count >= Configuration.Conditions.OpenMinimum)
                    OpenEvent();
                else TimedEventStart();
            });           
        }

        private void OpenEvent()
        {
            eventTimer?.Destroy();
            eventTimer = timer.In(Configuration.Timers.Open, StartEvent);

            timerExpire = CurrentTime() + Configuration.Timers.Open;

            status = EventStatus.Open;

            RefreshTimer(EventStatus.Open, "UI.OpenMessage");

            BroadcastAll("Notification.Open");            
        }

        private void StartEvent()
        {
            if (contestants.Count == 0)
            {
                BroadcastAll("Notification.NoContestants");
                TimedEventStart();
                return;
            }

            int required = (int)Math.Round(BasePlayer.activePlayerList.Count * Configuration.Conditions.Percentage, 0);
            if (contestants.Count < required)
            {
                BroadcastAll("Notification.NotEnoughContestants", Configuration.Conditions.Percentage * 100f);
                TimedEventStart();
                return;
            }

            if (!GetRandomDestination(out Vector3 start, out Vector3 finish))
            {
                Puts("Unable to find a valid juggernaut path. This is caused by players or buildings near too many potential destinations. Recommend to reload the plugin and generate new paths");
                return;
            }

            juggernaut = GetRandomPlayer();

            contestants.Clear();

            if (!juggernaut)
            {          
                TimedEventStart();
                return;
            }

            Interface.Call("OnJuggernautEventStarted", juggernaut);

            SubscribeHooks();

            FinishZone.Create(finish);            

            PrepareJuggernaut(juggernaut, start);

            CreateTargetMapMarker(finish, "Destination");

            float travelTime = CalculateAllowedTime(start, finish);

            timerExpire = CurrentTime() + travelTime;

            eventTimer?.Destroy();
            eventTimer = timer.In(travelTime, ()=> OnEventLose(true));

            BroadcastAll("Notification.JuggernautSelected", juggernaut.displayName);

            BroadcastAll("Notification.EventStarted");

            SendGameTip(juggernaut, msg("Tip.Compass", juggernaut.userID), 20);

            CreateMapMarkers(finish);

            CreateSignalFlare(finish, travelTime);

            SendApproximatePositionNotification();

            status = EventStatus.Started;

            RefreshTimer(EventStatus.Started, "UI.ProgressMessage");
        }

        private void OnEventWin()
        {
            if (Configuration.Prizes.Loot.Enabled)
            {
                List<ItemData> items = Pool.Get<List<ItemData>>();
                Configuration.Prizes.Loot.PopulateLoot(items);
                prizeData.Data.AddData(juggernaut, items);
                Pool.FreeUnmanaged(ref items);
            }
            else prizeData.Data.AddData(juggernaut);

            BroadcastAll("Notification.OnWin.Global");

            if (Configuration.Prizes.Inventory)
                SendReply(juggernaut, msg("Notification.OnWin.Prize", juggernaut.userID));

            if (Configuration.Prizes.Amount > 0)
            {
                if (Configuration.Prizes.Economics && Economics.IsLoaded)
                    Economics.Deposit(juggernaut.userID, (double)Configuration.Prizes.Amount);
                if (Configuration.Prizes.ServerRewards && ServerRewards.IsLoaded)
                    ServerRewards.AddPoints(juggernaut.userID, Configuration.Prizes.Amount);
            }

            NextTick(EndEvent);
        }

        private void OnEventLose(bool timeExpired)
        {
            if (timeExpired)
                BroadcastAll("Notification.TimeExpired");

            EndEvent();
        }

        private void EndEvent()
        {
            UnsubscribeHooks();

            if (signalFlare && !signalFlare.IsDestroyed)
                signalFlare.Kill();

            if (finishZone?.gameObject)
                UnityEngine.Object.Destroy(finishZone.gameObject);

            DestroyMarkers();
            ClearJuggernautMarkers();

            juggernautTeamMembers.Clear();
            
            status = EventStatus.Finished;

            BasePlayer player = juggernaut;

            if (player)
            {
                juggernaut = null;

                player.DirtyPlayerState();
                player.TeamUpdate();

                RelationshipManager.PlayerTeam playerTeam = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                playerTeam?.RemovePlayer(player.userID);

                player.ClearTeam();
                
                UnlockInventory(player);

                restoreData.Data.Restore(player);  
                restoreData.Save();
            }

            DestroyUI();
            
            Interface.Call("OnJuggernautEventStopped");

            TimedEventStart();
        }
        #endregion

        #region Juggernaut Management
        private BasePlayer GetRandomPlayer()
        {
            List<ulong> tempList = new List<ulong>(contestants);

            for (int i = 0; i < tempList.Count; i++)
            {
                ulong userID = contestants.GetRandom();
                tempList.Remove(userID);

                BasePlayer player = BasePlayer.FindByID(userID);
                if (player && !player.IsDead())
                    return player;
            }

            return null;
        }

        private void PrepareJuggernaut(BasePlayer player, Vector3 start)
        {
            CollectFriendIDs(player);

            restoreData.Data.Store(player);

            SaveData();

            player.StripInventory();

            if (Configuration.Juggernaut.ResetMetabolism)
            {
                player.metabolism.Reset();
                player.health = player.MaxHealth();
                player.metabolism.hydration.value = player.metabolism.hydration.max;
                player.metabolism.calories.value = player.metabolism.calories.max;
                player.metabolism.SendChangesToClient();
            }

            CreateJuggernautInventory(player);            

            player.Teleport(start, Quaternion.identity, true);

            player.Invoke(JuggernautTick, 1f);
        }

        private void CreateJuggernautInventory(BasePlayer player)
        {
            foreach (ConfigData.JuggernautOptions.InventoryItem inventoryItem in Configuration.Juggernaut.Inventory)
            {
                Item item = CreateItem(inventoryItem);

                if (item == null)
                    PrintError($"Error creating item: {inventoryItem.Shortname}. Check this is a correct shortname!");
                else
                {                    
                    if (item.contents != null && inventoryItem.Inventory?.Length > 0)
                    {
                        foreach (string attachmentShortname in inventoryItem.Inventory)
                        {
                            Item item2 = ItemManager.CreateByName(attachmentShortname, 1);
                            if (item2 != null)
                            {
                                item2.MoveToContainer(item.contents);
                            }
                        }
                    }
                    item.MoveToContainer(
                        inventoryItem.Container == "wear" ? player.inventory.containerWear : 
                        inventoryItem.Container == "belt" ? player.inventory.containerBelt : 
                        player.inventory.containerMain, inventoryItem.Slot, false);

                }
            }
            LockInventory(player);
        }

        private Item CreateItem(ConfigData.JuggernautOptions.InventoryItem inventoryItem)
        {
            Item item = null;
            if (inventoryItem.IsBP)
            {
                item = ItemManager.CreateByName("blueprintbase", inventoryItem.Amount, inventoryItem.SkinID);
                item.blueprintTarget = ItemManager.itemList.Find(x => x.shortname == inventoryItem.Shortname)?.itemid ?? 0;
            }
            else item = ItemManager.CreateByName(inventoryItem.Shortname, inventoryItem.Amount, inventoryItem.SkinID);
            
            BaseEntity heldEntity = item.GetHeldEntity();
            if (!heldEntity)
            {
                ItemModEntity itemModEntity = item.info.GetComponent<ItemModEntity>();
                heldEntity = itemModEntity ? itemModEntity.entityPrefab.GetEntity() : null;
            }

            if (heldEntity is BaseProjectile baseProjectile)
                baseProjectile.primaryMagazine.contents = baseProjectile.primaryMagazine.capacity;

            return item;
        }

        private void CollectFriendIDs(BasePlayer player)
        {
            RelationshipManager.PlayerTeam playerTeam = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
            if (playerTeam != null)
                juggernautTeamMembers.AddRange(playerTeam.members);

            if (Clans.IsLoaded)
            {
                string str = Clans.GetClanOf(player.userID);

                if (!string.IsNullOrEmpty(str))
                {
                    JObject clan = Clans.GetClan(str);
                    JArray members = clan?["members"] as JArray;
                    if (members != null)
                    {
                        for (int i = 0; i < members.Count; i++)
                        {
                            juggernautTeamMembers.Add(ulong.Parse(members[i].ToString()));
                        }
                    }
                }
            }

            if (Friends.IsLoaded)
            {
                ulong[] friends = Friends.GetFriends(player.userID);
                if (friends is { Length: > 0 })
                    juggernautTeamMembers.AddRange(friends);
            }
        }

        private void CreateTargetMapMarker(Vector3 position, string text)
        {
            juggernaut.DirtyPlayerState();
            juggernaut.TeamUpdate();

            RelationshipManager.PlayerTeam playerTeam = RelationshipManager.ServerInstance.FindTeam(juggernaut.currentTeam);
            playerTeam?.RemovePlayer(juggernaut.userID);

            juggernaut.ClearTeam();

            RelationshipManager.ServerInstance.playerToTeam.Remove(juggernaut.userID);

            RelationshipManager.PlayerTeam juggernautTeam = RelationshipManager.ServerInstance.CreateTeam();

            juggernautTeam.AddPlayer(juggernaut);

            SendMarkerUpdate(text);
        }

        private void SendMarkerUpdate(string text)
        {
            if (!juggernaut)
                return;

            MapNote mapNote = Pool.Get<MapNote>();
            mapNote.noteType = 1;
            mapNote.worldPosition = FinishZone.Position;
            mapNote.label = text;
            
            using (MapNoteList list = Pool.Get<MapNoteList>())
            {
                list.notes = Pool.Get<List<MapNote>>();

                if (juggernaut.ServerCurrentDeathNote != null)                
                    list.notes.Add(juggernaut.ServerCurrentDeathNote);

                list.notes.Add(mapNote);

                juggernaut.ClientRPCPlayer<MapNoteList>(null, juggernaut, "Client_ReceiveMarkers", list);
                list.notes.Clear();
            }            
        }

        private void JuggernautTick()
        {
            if (!Configuration.Juggernaut.HurtVehicles || !juggernaut || juggernaut.IsDead())
                return;

            if (juggernaut.HasParent())
            {
                BaseCombatEntity parentEntity = juggernaut.GetParentEntity() as BaseCombatEntity;
                if (parentEntity && parentEntity is BaseVehicle or BaseVehicleModule)
                {
                    juggernaut.ChatMessage(msg("Notification.VehicleDamage", juggernaut.userID));
                    parentEntity.Hurt(parentEntity.MaxHealth() * 0.1f, Rust.DamageType.Explosion, juggernaut, false);
                }
            }

            juggernaut.Invoke(JuggernautTick, 1f);
        }
        #endregion

        #region Helpers       
        
        private void LockInventory(BasePlayer player)
        {
            player.inventory.containerMain.SetFlag(ItemContainer.Flag.IsLocked, true);
            player.inventory.containerBelt.SetFlag(ItemContainer.Flag.IsLocked, true);
            player.inventory.containerWear.SetFlag(ItemContainer.Flag.IsLocked, true);

            player.inventory.SendSnapshot();
        }

        private static void UnlockInventory(BasePlayer player)
        {
            player.inventory.containerMain.SetFlag(ItemContainer.Flag.IsLocked, false);
            player.inventory.containerBelt.SetFlag(ItemContainer.Flag.IsLocked, false);
            player.inventory.containerWear.SetFlag(ItemContainer.Flag.IsLocked, false);

            player.inventory.SendSnapshot();
        }

        private Color ConvertToColor(string color)
        {
            if (color.StartsWith("#"))
                color = color.Substring(1);
            int red = int.Parse(color.Substring(0, 2), NumberStyles.AllowHexSpecifier);
            int green = int.Parse(color.Substring(2, 2), NumberStyles.AllowHexSpecifier);
            int blue = int.Parse(color.Substring(4, 2), NumberStyles.AllowHexSpecifier);

            return new Color((float)red / 255, (float)green / 255, (float)blue / 255);
        }
        #endregion

        #region Messaging
        private void BroadcastAll(string key, params object[] args)
        {
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer player = BasePlayer.activePlayerList[i];

                SendReply(player, args != null ? string.Format(msg(key, player.userID), args) : msg(key, player.userID));
            }
        }

        private void SendGameTip(BasePlayer player, string message, int time = 10)
        {
            if (!player)
                return;

            float yMin = Configuration.UI.Position.YPosition + Configuration.UI.Position.YDimension + 0.0025f;
            float yMax = yMin + 0.04f;

            CuiElementContainer container = UI.Container(UI_NOTIFICATION, UI.Color(Configuration.UI.Color, Configuration.UI.Opacity), $"{Configuration.UI.Position.XPosition} {yMin}", $"{Configuration.UI.Position.XPosition + Configuration.UI.Position.XDimension} {yMax}", false);
            UI.Label(ref container, UI_NOTIFICATION, $"ⓘ {message}", 12, "0 0", "1 1");
            CuiHelper.DestroyUi(player, UI_NOTIFICATION);
            CuiHelper.AddUi(player, container);

            player.Invoke(() => CuiHelper.DestroyUi(player, UI_NOTIFICATION), time);

            //player.SendConsoleCommand("gametip.showgametip", message);

            //player.Invoke(() => player.SendConsoleCommand("gametip.hidegametip"), time);
            player.SendConsoleCommand("gametip.hidegametip");
        }

        private void SendApproximatePositionNotification()
        {            
            timer.In(Configuration.Game.BroadcastEvery, () =>
            {
                if (status != EventStatus.Started)
                    return;

                if (Configuration.Game.BroadcastEvery < 1)
                    return;

                if (!juggernaut || juggernaut.IsDead())
                    return;

                BroadcastAll("Notification.Position", MapHelper.PositionToString(juggernaut.transform.position));

                SendApproximatePositionNotification();
            });
        }
        #endregion

        #region Plugin Intergration
        private object CanTrade(BasePlayer player)
        {
            if (player == juggernaut)
                return msg("Notification.OnTrade", player.userID);
            return null;
        }

        private object canRemove(BasePlayer player)
        {
            if (player == juggernaut)
                return msg("Notification.OnRemove", player.userID);
            return null;
        }

        private object CanTeleport(BasePlayer player)
        {
            if (player == juggernaut)
                return msg("Notification.OnTP", player.userID);
            return null;
        }

        private object canShop(BasePlayer player)
        {
            if (player == juggernaut)
                return msg("Notification.OnShop", player.userID);
            return null;
        }

        private object CanRedeemKit(BasePlayer player)
        {
            if (player == juggernaut)
                return msg("Notification.OnKit", player.userID);
            return null;
        }

        private object IsEventPlayer(BasePlayer player) => player == juggernaut ? (object)true : null;

        private object isEventPlayer(BasePlayer player) => player == juggernaut ? (object)true : null;

        #endregion

        #region Path Generation
        private const int VIS_RAYCAST_LAYERS = 1 << 8 | 1 << 17 | 1 << 21;

        private const int POINT_RAYCAST_LAYERS = 1 << 4 | 1 << 8 | 1 << 10 | 1 << 15 | 1 << 16 | 1 << 21 | 1 << 23 | 1 << 27 | 1 << 28 | 1 << 29;
        
        private const int BLOCKED_TOPOLOGY = (int)(TerrainTopology.Enum.Cliff | TerrainTopology.Enum.Cliffside | TerrainTopology.Enum.Lake | TerrainTopology.Enum.Ocean | TerrainTopology.Enum.Monument | TerrainTopology.Enum.Offshore | TerrainTopology.Enum.River | TerrainTopology.Enum.Swamp | TerrainTopology.Enum.Rail | TerrainTopology.Enum.Railside);

        private void GenerateDestinations()
        {
            int blockedTopology = 0;
            for (int i = 0; i < Configuration.Generation.BlockedTopologies.Length; i++)
            {
                if (Enum.TryParse(Configuration.Generation.BlockedTopologies[i], out TerrainTopology.Enum value))                
                    blockedTopology |= (int)value;                
                else Debug.Log($"[Juggernaut] Failed to parse topology type {Configuration.Generation.BlockedTopologies[i]}");
            }
            
            List<Vector3> positions = Pool.Get<List<Vector3>>();

            float halfSize = (float)World.Size * 0.5f;

            for (int i = 0; i < Configuration.Generation.Attempts; i++)
            {
                Vector2 random = (UnityEngine.Random.insideUnitCircle * 0.95f) * halfSize;

                Vector3 position = new Vector3(random.x, 500f, random.y);

                if (ContainsTopologyAtPoint(BLOCKED_TOPOLOGY, position))
                    continue;
                
                if (!IsPointOnTerrain(position, out float heightAtPoint) || !IsValidSlopeAtPoint(position) || IsPositionInZone(position))
                    continue;

                position.y = heightAtPoint;

                positions.Add(position);
            }
            
            float min = TerrainMeta.Size.x / Configuration.Generation.MinTravelDistance;
            
            if (positions.Count < 2)
            {
                PrintError("Failed to generate valid destinations! Reload the plugin and try again");
                return;
            }

            for (int i = 0; i < Configuration.Generation.Attempts; i++)
            {
                Vector3 start = positions.GetRandom();
                Vector3 end = positions.GetRandom();

                if (Vector3.Distance(start, end) >= min)
                {
                    destinations.Add(new KeyValuePair<Vector3, Vector3>(start, end));
                    positions.Remove(start);
                    positions.Remove(end);
                }
            }

            Puts("Generated {0} possible Juggernaut paths", destinations.Count);

            Pool.FreeUnmanaged(ref positions);
        }
        
        private bool ContainsTopologyAtPoint(int mask, Vector3 position) => (TerrainMeta.TopologyMap.GetTopology(position) & mask) != 0;

        private bool IsValidSlopeAtPoint(Vector3 position) => TerrainMeta.HeightMap.GetSlope(position) <= Configuration.Generation.MaxSlope;
        private bool IsPointOnTerrain(Vector3 position, out float heightAtPoint)
        {
            if (Physics.Raycast(position, Vector3.down, out RaycastHit raycastHit, 500f, POINT_RAYCAST_LAYERS))
            {
                if (raycastHit.collider is TerrainCollider)
                {
                    heightAtPoint = raycastHit.point.y;
                    return true;
                }
            }
            heightAtPoint = 500f;
            return false;
        }
        
        private bool IsPositionInZone(Vector3 position)
        {
            if (!ZoneManager.IsLoaded || Configuration.Generation.BlockedZones.Length == 0)
                return false;

            for (int i = 0; i < Configuration.Generation.BlockedZones.Length; i++)
            {
                bool success = ZoneManager.IsPositionInZone(Configuration.Generation.BlockedZones[i], position);
                if (success)
                    return true;
            }
            return false;
        }

        private void ShowDestinations(BasePlayer player, int time)
        {
            if (!player)
                return;

            for (int i = 0; i < destinations.Count; i++)
            {
                KeyValuePair<Vector3, Vector3> kvp = destinations[i];

                player.SendConsoleCommand("ddraw.text", time, Color.green, kvp.Key, i.ToString());
                player.SendConsoleCommand("ddraw.text", time, Color.green, kvp.Value, i.ToString());
                player.SendConsoleCommand("ddraw.line", time, Color.green, kvp.Key, kvp.Value);
            }
        }

        private bool GetRandomDestination(out Vector3 start, out Vector3 finish)
        {
            List<BaseEntity> entities = Pool.Get<List<BaseEntity>>();

            start = finish = Vector3.zero;

            bool success = false;

            for (int i = 0; i < 50; i++)
            {
                KeyValuePair<Vector3, Vector3> kvp = destinations.GetRandom();

                if (IsPositionInZone(kvp.Key) || IsPositionInZone(kvp.Value))
                    continue;
                
                entities.Clear();
                
                Vis.Entities(kvp.Key, Configuration.Generation.BuildingDistance, entities, VIS_RAYCAST_LAYERS);
                
                if (entities.Count > 0)
                    continue;
                
                entities.Clear();
                Vis.Entities(kvp.Value, Configuration.Generation.BuildingDistance, entities, VIS_RAYCAST_LAYERS);
                
                if (entities.Count > 0)
                    continue;

                start = kvp.Key;
                finish = kvp.Value;
                success = true;
                break;
            }

            Pool.FreeUnmanaged(ref entities);

            return success;
        }

        private float CalculateAllowedTime(Vector3 start, Vector3 finish) => Vector3.Distance(start, finish) / Configuration.Timers.Completion;        
        #endregion

        #region Finish Zone
        private class FinishZone : MonoBehaviour
        {
            internal static Vector3 Position { get; private set; }

            internal static void Create(Vector3 position)
            {
                if (!Instance.finishZone)
                    Instance.finishZone = new GameObject().AddComponent<FinishZone>();

                Position = Instance.finishZone.transform.position = position;                  
            }

            private void Awake()
            {
                gameObject.name = "Juggernaut-Finish";
                gameObject.layer = (int)Rust.Layer.Reserved1;

                SphereCollider collider = gameObject.AddComponent<SphereCollider>();
                collider.isTrigger = true;
                collider.radius = 1f;
            }

            private void OnTriggerEnter(Collider collider)
            {
                if (!collider || !collider.gameObject || collider.gameObject.layer != (int)Rust.Layer.Player_Server)
                    return;

                BasePlayer player = collider.GetComponentInParent<BasePlayer>();
                if (!player)
                    return;

                if (player == Instance.juggernaut)
                    Instance.OnEventWin();
            }

            private void OnDestroy() => Position = Vector3.zero;
        }

        private void CreateSignalFlare(Vector3 destination, float destroyIn)
        {
            if (signalFlare && !signalFlare.IsDestroyed)
                signalFlare.Kill();

            signalFlare = GameManager.server.CreateEntity(FLARE_PREFAB, destination + (Vector3.up * 0.1f), Quaternion.identity) as RoadFlare;
            signalFlare.enableSaving = false;
            signalFlare.Spawn();

            signalFlare.waterCausesExplosion = false;
            signalFlare.CancelInvoke(signalFlare.Explode);
            signalFlare.Invoke(signalFlare.Explode, destroyIn);

            Rigidbody rigidBody = signalFlare.GetComponent<Rigidbody>();
            rigidBody.isKinematic = true;
        }

        private void CreateMapMarkers(Vector3 destination)
        {
            if (Configuration.Markers.Amount < 1)
                return;

            for (int i = 0; i < Configuration.Markers.Amount; i++)
            {
                Vector3 possibleDestination = Vector3.zero;

                if (i != 0)
                {
                    for (int y = 0; y < 50; y++)
                    {
                        Vector3 finish = destinations.GetRandom().Value;
                        if (Vector3.Distance(finish, destination) <= 750)
                        {
                            possibleDestination = finish;
                            break;
                        }
                    }
                    if (possibleDestination == Vector3.zero)
                        possibleDestination = destinations.GetRandom().Value;
                }
                else possibleDestination = destination;

                MapMarkerGenericRadius mapMarker = (MapMarkerGenericRadius)GameManager.server.CreateEntity(MARKER_PREFAB, possibleDestination + (UnityEngine.Random.onUnitSphere * (Configuration.Markers.Radius * 70f)), Quaternion.identity);
                mapMarker.enableSaving = false;
                mapMarker.Spawn();

                mapMarker.radius = Configuration.Markers.Radius;
                mapMarker.alpha = Configuration.Markers.Alpha;

                mapMarker.color1 = mapMarker.color2 = ConvertToColor(Configuration.Markers.Color);

                mapMarker.SendUpdate();

                markers.Add(mapMarker);

                possibleDestination = Vector3.zero;
            }
        }

        private void DestroyMarkers()
        {
            for (int i = 0; i < markers.Count; i++)
            {
                MapMarkerGenericRadius mapMarker = markers[i];
                if (mapMarker && !mapMarker.IsDestroyed)
                    mapMarker.Kill();
            }

            markers.Clear();
        }

        private void ClearJuggernautMarkers()
        {
            if (!juggernaut || juggernaut.State == null)
                return;
            
            for (int i = juggernaut.State.pointsOfInterest.Count - 1; i >= 0; i--)
            {
                MapNote mapNote = juggernaut.State.pointsOfInterest[i];
                if (mapNote is { label: "Destination" })
                {
                    mapNote.Dispose();
                    juggernaut.State.pointsOfInterest.RemoveAt(i);
                }
            }
           
            juggernaut.DirtyPlayerState();
            juggernaut.TeamUpdate();
            juggernaut.SendMarkersToClient();
        }
        #endregion

        #region UI
        public class UI
        {
            public static CuiElementContainer Container(string panelName, string color, string aMin, string aMax, bool useCursor = false)
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color},
                            RectTransform = {AnchorMin = aMin, AnchorMax = aMax},
                            CursorEnabled = useCursor
                        },
                        new CuiElement().Parent = "Hud",
                        panelName
                    }
                };
                return container;
            }
           
            public static void Label(ref CuiElementContainer container, string panel, string text, int size, string aMin, string aMax, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = aMin, AnchorMax = aMax }
                },
                panel);
            }

            public static string Color(string color, float alpha)
            {
                if (color.StartsWith("#"))
                    color = color.Substring(1);
                int red = int.Parse(color.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(color.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(color.Substring(4, 2), NumberStyles.AllowHexSpecifier);

                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }

        private void RefreshTimer(EventStatus currentStatus, string key)
        {
            if (status != currentStatus)
            {
                DestroyUI();
                return;
            }

            string color = UI.Color(Configuration.UI.Color, Configuration.UI.Opacity);
            string min = Configuration.UI.Position.Min();
            string max = Configuration.UI.Position.Max();

            string time = FormatTime(timerExpire - CurrentTime());

            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer player = BasePlayer.activePlayerList[i];

                CuiElementContainer container = UI.Container(UI_PANEL, color, min, max);
                UI.Label(ref container, UI_PANEL, string.Format(msg(key, player.userID), time), Configuration.UI.Size, "0 0", "1 1");

                CuiHelper.DestroyUi(player, UI_PANEL);
                CuiHelper.AddUi(player, container);
            }

            timer.In(1f, () => RefreshTimer(currentStatus, key));
        }

        private void DestroyUI()
        {
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                CuiHelper.DestroyUi(BasePlayer.activePlayerList[i], UI_PANEL);
            }
        }
        #endregion

        #region Commands
        [ChatCommand("juggernaut")]
        private void cmdJuggernaut(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                SendReply(player, msg("Chat.Help.Enter", player.userID));
                SendReply(player, msg("Chat.Help.Leave", player.userID));

                if (Configuration.Prizes.Inventory)
                    SendReply(player, msg("Chat.Help.Claim", player.userID));

                if (player.IsAdmin)
                {
                    SendReply(player, msg("Chat.Help.Open", player.userID));
                    SendReply(player, msg("Chat.Help.Start", player.userID));
                    SendReply(player, msg("Chat.Help.Cancel", player.userID));
                }
                return;
            }

            switch (args[0].ToLower())
            {
                case "enter":
                    if (status != EventStatus.Open)
                    {
                        SendReply(player, msg("Chat.Error.NoEventInProgress", player.userID));
                        return;
                    }

                    if (!permission.UserHasPermission(player.UserIDString, JUGGERNAUT_PERMISSION))
                    {
                        SendReply(player, msg("Chat.Error.NoPermission", player.userID));
                        return;
                    }

                    if (contestants.Contains(player.userID))
                    {
                        SendReply(player, msg("Chat.Enter.AlreadyAContestant", player.userID));
                        return;
                    }

                    contestants.Add(player.userID);
                    SendReply(player, msg("Chat.Enter.Added", player.userID));

                    BroadcastAll("Notification.PlayerJoined", player.displayName, contestants.Count);
                    return;

                case "leave":
                    if (status != EventStatus.Open)
                    {
                        SendReply(player, msg("Chat.Error.NoEventInProgress", player.userID));
                        return;
                    }

                    if (!contestants.Contains(player.userID))
                    {
                        SendReply(player, msg("Chat.Leave.NotAContestant", player.userID));
                        return;
                    }

                    contestants.Remove(player.userID);
                    SendReply(player, msg("Chat.Leave.Removed", player.userID));

                    BroadcastAll("Notification.PlayerLeft", player.displayName, contestants.Count);
                    return;

                case "claim":
                    if (!Configuration.Prizes.Inventory)
                        return;

                    if (!prizeData.Data.FindDataForPlayer(player, out PrizeData.WinnerData data))
                    {
                        SendReply(player, msg("Chat.Claim.NoData", player.userID));
                        return;
                    }

                    timer.Once(0.15f, () => OpenClaimContainer(player, data));
                    
                    //SendReply(player, msg("Chat.Claim.Success", player.userID));
                    //SaveData();
                    return;

                case "open":
                    if (!player.IsAdmin)
                    {
                        SendReply(player, msg("Chat.Error.NoPermission", player.userID));
                        return;
                    }

                    if (status != EventStatus.Finished)
                    {
                        SendReply(player, msg("Chat.Error.EventInProgress", player.userID));
                        return;
                    }

                    OpenEvent();
                    SendReply(player, msg("Chat.Open.Success", player.userID));
                    return;

                case "start":
                    if (!player.IsAdmin)
                    {
                        SendReply(player, msg("Chat.Error.NoPermission", player.userID));
                        return;
                    }

                    if (status != EventStatus.Open)
                    {
                        SendReply(player, msg("Chat.Error.NoEventOpen", player.userID));
                        return;
                    }

                    if (contestants.Count == 0)
                    {
                        SendReply(player, msg("Chat.Error.NoContestants", player.userID));
                        return;
                    }

                    StartEvent();
                    SendReply(player, msg("Chat.Start.Success", player.userID));
                    return;

                case "cancel":
                    if (!player.IsAdmin)
                    {
                        SendReply(player, msg("Chat.Error.NoPermission", player.userID));
                        return;
                    }

                    if (status == EventStatus.Finished)
                    {
                        SendReply(player, msg("Chat.Error.NoEventInProgress", player.userID));
                        return;
                    }

                    EndEvent();
                    BroadcastAll("Notification.AdminCancelled");

                    SendReply(player, msg("Chat.Cancel.Success", player.userID));
                    return;
                
                case "viewdest":
                    if (!player.IsAdmin)
                    {
                        SendReply(player, msg("Chat.Error.NoPermission", player.userID));
                        return;
                    }

                    ShowDestinations(player, 30);
                    return;

                default:
                    break;
            }
        }

        [ConsoleCommand("juggernaut")]
        private void cxmdJuggernaut(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
                return;

            if (arg.Args?.Length == 0)
            {
                SendReply(arg, "juggernaut open - Force open an event");
                SendReply(arg, "juggernaut start - Force start an event");
                SendReply(arg, "juggernaut cancel - Force finish a event");
                return;
            }

            switch (arg.Args[0].ToLower())
            {                
                case "open":                    
                    if (status != EventStatus.Finished)
                    {
                        SendReply(arg, "You can not open a event when one is already in progress");
                        return;
                    }

                    OpenEvent();
                    SendReply(arg, "You have force opened the event");
                    return;

                case "start":                    
                    if (status != EventStatus.Open)
                    {
                        SendReply(arg, "You must open a event before you can start one");
                        return;
                    }

                    if (contestants.Count == 0)
                    {
                        SendReply(arg, "Unable to start event. No contestants have registered to play");
                        return;
                    }

                    StartEvent();
                    SendReply(arg, "You have force started the event");
                    return;

                case "cancel":
                    if (status == EventStatus.Finished)
                    {
                        SendReply(arg, "There is currently no event in progress");
                        return;
                    }

                    EndEvent();
                    BroadcastAll("Notification.AdminCancelled");

                    SendReply(arg, "You have force cancelled the event");                    
                    return;

                default:
                    break;
            }
        }
        #endregion

        #region Config        
        private ConfigData Configuration => ConfigurationData as ConfigData;


        protected override ConfigurationFile OnLoadConfig(ref ConfigurationFile configurationFile) => configurationFile = new ConfigurationFile<ConfigData>(Config);
        
        protected class ConfigData : BaseConfigData
        {
            [JsonProperty(PropertyName = "Juggernaut Settings")]
            public JuggernautOptions Juggernaut { get; set; }

            [JsonProperty(PropertyName = "Event Timers")]
            public EventTimers Timers { get; set; }

            [JsonProperty(PropertyName = "Map Markers")]
            public MapMarkerOptions Markers { get; set; }

            [JsonProperty(PropertyName = "UI Settings")]
            public UISettings UI { get; set; }

            [JsonProperty(PropertyName = "Event Conditions")]
            public EventConditions Conditions { get; set; }
           
            [JsonProperty(PropertyName = "Game Settings")]
            public GameOptions Game { get; set; }
            
            [JsonProperty(PropertyName = "Generation Settings")]
            public GenerationOptions Generation { get; set; }

            [JsonProperty(PropertyName = "Reward Settings")]
            public PrizeOptions Prizes { get; set; }

            public class JuggernautOptions
            {
                [JsonProperty(PropertyName = "Defense damage modifier")]
                public float DefenseModifier { get; set; }

                [JsonProperty(PropertyName = "Attack damage modifier")]
                public float AttackModifier { get; set; }

                [JsonProperty(PropertyName = "Can damage structures")]
                public bool DamageStructures { get; set; }

                [JsonProperty(PropertyName = "Can loot containers and players")]
                public bool CanLoot { get; set; }

                [JsonProperty(PropertyName = "Start with full metabolism")]
                public bool ResetMetabolism { get; set; }

                [JsonProperty(PropertyName = "Disable landmine damage")]
                public bool NoLandmineDamage { get; set; }

                [JsonProperty(PropertyName = "Disable beartrap damage")]
                public bool NoBeartrapDamage { get; set; }

                [JsonProperty(PropertyName = "Disable fall damage")]
                public bool NoFallDamage { get; set; }

                [JsonProperty(PropertyName = "Prevent mounting vehicles and animals")]
                public bool DisableMounting { get; set; }

                [JsonProperty(PropertyName = "Damage vehicles if juggernaut is riding in it (unmounted)")]
                public bool HurtVehicles { get; set; }

                [JsonProperty(PropertyName = "Inventory contents")]
                public List<InventoryItem> Inventory { get; set; }
                
                public class InventoryItem
                {
                    [JsonProperty(PropertyName = "Item shortname")]
                    public string Shortname { get; set; }

                    [JsonProperty(PropertyName = "Amount of item")]
                    public int Amount { get; set; }

                    [JsonProperty(PropertyName = "Item skin ID")]
                    public ulong SkinID { get; set; }

                    [JsonProperty(PropertyName = "Is this item a blueprint?")]
                    public bool IsBP { get; set; }

                    [JsonProperty(PropertyName = "Container (main, wear or belt)")]
                    public string Container { get; set; }

                    [JsonProperty(PropertyName = "Slot Number")]
                    public int Slot { get; set; }

                    [JsonProperty(PropertyName = "Item contents (shortname's)")]
                    public string[] Inventory { get; set; }
                }
            }
            
            public class MapMarkerOptions
            {
                [JsonProperty(PropertyName = "Show radial map markers on possible juggernaut destinations")]
                public bool ShowDestinations { get; set; }

                [JsonProperty(PropertyName = "Amount of possible destinations to show")]
                public int Amount { get; set; }

                [JsonProperty(PropertyName = "Marker radius")]
                public float Radius { get; set; }

                [JsonProperty(PropertyName = "Marker transparency (0.0 - 1.0)")]
                public float Alpha { get; set; }

                [JsonProperty(PropertyName = "Marker color (hex)")]
                public string Color { get; set; }
            }

            public class PrizeOptions
            {
                [JsonProperty(PropertyName = "Allow players to loot juggernaut as a prize")]
                public bool Inventory { get; set; }

                [JsonProperty(PropertyName = "Disallow looting juggernaut attire")]
                public bool RemoveAttire { get; set; }

                [JsonProperty(PropertyName = "Use Economics money as a prize")]
                public bool Economics { get; set; }

                [JsonProperty(PropertyName = "Use ServerRewards money as a prize")]
                public bool ServerRewards { get; set; }

                [JsonProperty(PropertyName = "Monetary amount")]
                public int Amount { get; set; }
                
                [JsonProperty(PropertyName = "Loot prizes (randomly generated, replaces inventory as prize)")]
                public RandomLoot Loot { get; set; }
                
                public class RandomLoot
                {
                    [JsonProperty(PropertyName = "Use random loot as prizes")]
                    public bool Enabled { get; set; }
                    
                    [JsonProperty(PropertyName = "Minimum amount of items to spawn")]
                    public int Minimum { get; set; }

                    [JsonProperty(PropertyName = "Maximum amount of items to spawn")]
                    public int Maximum { get; set; }

                    public List<LootDefinition> List { get; set; }

                    public class LootDefinition
                    {
                        public string Shortname { get; set; }
                        
                        public int Minimum { get; set; }

                        public int Maximum { get; set; }

                        public ulong SkinID { get; set; }

                        [JsonProperty(PropertyName = "Probability (0.0 - 1.0)")]
                        public float Probability { get; set; }

                        [JsonProperty(PropertyName = "Minimum condition (0.0 - 1.0)")]
                        public float MinCondition { get; set; } = 1f;

                        [JsonProperty(PropertyName = "Maximum condition (0.0 - 1.0)")]
                        public float MaxCondition { get; set; } = 1f;

                        [JsonProperty(PropertyName = "Spawn with")]
                        public LootDefinition Required { get; set; }

                        [JsonIgnore]
                        private ItemDefinition _blueprintDefinition;

                        [JsonIgnore]
                        private ItemDefinition BlueprintDefinition
                        {
                            get
                            {
                                if (!_blueprintDefinition)
                                    _blueprintDefinition = ItemManager.FindItemDefinition("blueprintbase");
                                return _blueprintDefinition;
                            }
                        }

                        private int GetAmount()
                        {
                            if (Maximum <= 0f || Maximum <= Minimum)
                                return Minimum;

                            return UnityEngine.Random.Range(Minimum, Maximum);
                        }

                        public void Create(BasePlayer player)
                        {
                            Item item = ItemManager.CreateByName(Shortname, GetAmount(), SkinID);

                            if (item != null)
                            {
                                item.conditionNormalized = UnityEngine.Random.Range(Mathf.Clamp01(MinCondition), Mathf.Clamp01(MaxCondition));

                                item.OnVirginSpawn();
                                player.GiveItem(item, BaseEntity.GiveItemReason.Generic);
                            }

                            if (Required != null)
                                Required.Create(player);
                        }
                        
                        public void Create(List<ItemData> items)
                        {
                            ItemDefinition itemDefinition = ItemManager.FindItemDefinition(Shortname);
                            ItemData item = new ItemData();
                            
                            item.Shortname = itemDefinition.shortname;
                            item.Amount = GetAmount();
                            item.Skin = SkinID;
                            if (itemDefinition.condition.enabled)
                            {
                                item.Condition = UnityEngine.Random.Range(Mathf.Clamp01(MinCondition), Mathf.Clamp01(MaxCondition)) * itemDefinition.condition.max;
                                item.MaxCondition = itemDefinition.condition.max;
                            }

                            items.Add(item);

                            Required?.Create(items);
                        }
                    }
                    
                    public void PopulateLoot(BasePlayer player)
                    {
                        if (!player)
                            return;
            
                        int count = UnityEngine.Random.Range(Minimum, Maximum);

                        int spawnedCount = 0;
                        int loopCount = 0;

                        while (true)
                        {
                            loopCount++;

                            if (loopCount > 3)
                                break;

                            float probability = UnityEngine.Random.Range(0f, 1f);

                            List<LootDefinition> definitions = new List<LootDefinition>(List);

                            for (int i = 0; i < List.Count; i++)
                            {
                                LootDefinition lootDefinition = definitions.GetRandom();

                                definitions.Remove(lootDefinition);

                                if (lootDefinition.Probability >= probability)
                                {
                                    lootDefinition.Create(player);

                                    spawnedCount++;

                                    if (spawnedCount >= count)
                                        break;
                                }
                            }
                        }

                    }

                    public void PopulateLoot(List<ItemData> items)
                    {
                        int count = UnityEngine.Random.Range(Minimum, Maximum);

                        int spawnedCount = 0;
                        int loopCount = 0;

                        while (true)
                        {
                            loopCount++;

                            if (loopCount > 3)
                                break;

                            float probability = UnityEngine.Random.Range(0f, 1f);

                            List<LootDefinition> definitions = new List<LootDefinition>(List);

                            for (int i = 0; i < List.Count; i++)
                            {
                                LootDefinition lootDefinition = definitions.GetRandom();

                                definitions.Remove(lootDefinition);

                                if (lootDefinition.Probability >= probability)
                                {
                                    lootDefinition.Create(items);

                                    spawnedCount++;

                                    if (spawnedCount >= count)
                                        break;
                                }
                            }
                        }
                    }
                }
            }

            public class EventTimers
            {
                [JsonProperty(PropertyName = "Amount of time to complete journey (distance per second)")]
                public float Completion { get; set; }

                [JsonProperty(PropertyName = "Amount of time between events (seconds)")]
                public int Interval { get; set; }

                [JsonProperty(PropertyName = "Amount of time the entry process will remain open (seconds)")]
                public int Open { get; set; }
            }

            public class EventConditions
            {
                [JsonProperty(PropertyName = "The percentage of server players required for the event to start")]
                public float Percentage { get; set; }

                [JsonProperty(PropertyName = "The minimum amount of players on the server required to open the event")]
                public int OpenMinimum { get; set; }
            }

            public class GameOptions
            {
                [JsonProperty(PropertyName = "Broadcast the juggernauts approximate position to chat every X seconds (0 to disable)")]
                public int BroadcastEvery { get; set; }                

                [JsonProperty(PropertyName = "Blacklisted commands for event players")]
                public string[] CommandBlacklist { get; set; }
            }

            public class GenerationOptions
            {
                [JsonProperty(PropertyName = "Origin/destination point maximum ground slope (degrees)")]
                public float MaxSlope { get; set; }

                [JsonProperty(PropertyName = "Origin/destination point minimum distance from buildings (metres)")]
                public float BuildingDistance { get; set; }
                
                [JsonProperty(PropertyName = "Origin/destination combination generation attempts")]
                public int Attempts { get; set; }
                
                [JsonProperty(PropertyName = "Minimum travel distance calculation (World size / this number)")]
                public float MinTravelDistance { get; set; }
                
                [JsonProperty(PropertyName = "Disable origin/destination points in these zones (zone IDs)")]
                public string[] BlockedZones { get; set; }
                
                [JsonProperty(PropertyName = "Disable origin/destination points in these topologies")]
                public string[] BlockedTopologies { get; set; }
            }

            public class UISettings
            {               
                [JsonProperty(PropertyName = "Display a timer showing how long the juggernaut has to get to their destination")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Timer positioning")]
                public CUIPosition Position { get; set; }

                [JsonProperty(PropertyName = "UI background color (hex)")]
                public string Color { get; set; }

                [JsonProperty(PropertyName = "UI opacity (0.0 - 1.0)")]
                public float Opacity { get; set; }

                [JsonProperty(PropertyName = "Text size")]
                public int Size { get; set; }

                public class CUIPosition
                {
                    [JsonProperty(PropertyName = "Horizontal start position (left)")]
                    public float XPosition { get; set; }

                    [JsonProperty(PropertyName = "Vertical start position (bottom)")]
                    public float YPosition { get; set; }

                    [JsonProperty(PropertyName = "Horizontal dimensions")]
                    public float XDimension { get; set; }

                    [JsonProperty(PropertyName = "Vertical dimensions")]
                    public float YDimension { get; set; }

                    public string Min() => $"{XPosition} {YPosition}";

                    public string Max() => $"{XPosition + XDimension} {YPosition + YDimension}";
                }
            }
            public Oxide.Core.VersionNumber Version { get; set; }
        }
        
        protected override T GenerateDefaultConfiguration<T>()
        {
            return new ConfigData
            {
                Conditions = new ConfigData.EventConditions
                {
                    OpenMinimum = 10,
                    Percentage = 0.25f
                },
                Game = new ConfigData.GameOptions
                {
                    BroadcastEvery = 60,
                    CommandBlacklist = new string[]
                    {
                        "s",
                        "tp",
                        "tpa",
                        "tpr",
                        "home"
                    }
                },
                Generation = new ConfigData.GenerationOptions
                {
                    MaxSlope = 45f,
                    BuildingDistance = 20f,
                    Attempts = 3000,
                    MinTravelDistance = 4f,
                    BlockedZones = new string[0],
                    BlockedTopologies = new string[] { "Cliff", "Cliffside", "Lake", "Ocean", "Monument", "Offshore", "River", "Swamp", "Rail" },
                },
                Juggernaut = new ConfigData.JuggernautOptions
                {
                    AttackModifier = 1.25f,
                    CanLoot = true,
                    DamageStructures = true,
                    DisableMounting = true,
                    ResetMetabolism = true,
                    DefenseModifier = 0.5f,
                    NoBeartrapDamage = false,
                    NoLandmineDamage = false,
                    NoFallDamage = false,
                    HurtVehicles = true,
                    Inventory = new List<ConfigData.JuggernautOptions.InventoryItem>
                    {                        
                        new ConfigData.JuggernautOptions.InventoryItem
                        {
                            Amount = 1,
                            Shortname = "scientistsuit_heavy",
                            SkinID = 0,
                            IsBP = false,
                            Container = "wear",
                            Slot = -1
                        },
                        new ConfigData.JuggernautOptions.InventoryItem
                        {
                            Amount = 1,
                            Shortname = "lmg.m249",
                            SkinID = 0,
                            IsBP = false,
                            Container = "belt",
                            Slot = 0,
                            Inventory = new string[]{ "weapon.mod.lasersight", "weapon.mod.muzzleboost" }
                        },
                        new ConfigData.JuggernautOptions.InventoryItem
                        {
                            Amount = 500,
                            Shortname = "ammo.rifle.explosive",
                            SkinID = 0,
                            IsBP = false,
                            Container = "main",
                            Slot = -1
                        },
                        new ConfigData.JuggernautOptions.InventoryItem
                        {
                            Amount = 3,
                            Shortname = "grenade.f1",
                            SkinID = 0,
                            IsBP = false,
                            Container = "belt",
                            Slot = 1
                        },
                        new ConfigData.JuggernautOptions.InventoryItem
                        {
                            Amount = 3,
                            Shortname = "syringe.medical",
                            SkinID = 0,
                            IsBP = false,
                            Container = "belt",
                            Slot = 2
                        }
                    }
                },
                Markers = new ConfigData.MapMarkerOptions
                {
                    Alpha = 0.8f,
                    Amount = 3,
                    Color = "#ce422b",
                    Radius = 0.5f,
                    ShowDestinations = true
                },
                Prizes = new ConfigData.PrizeOptions
                {
                    Amount = 0,
                    Economics = false,
                    Inventory = true,
                    ServerRewards = false,
                    Loot = new ConfigData.PrizeOptions.RandomLoot
                    {
                        Enabled = false,
                        Minimum = 3,
                        Maximum = 6,
                        List = new List<ConfigData.PrizeOptions.RandomLoot.LootDefinition>()
                        {
                            new ConfigData.PrizeOptions.RandomLoot.LootDefinition
                            {
                                Shortname = "hazmatsuit",
                                Minimum = 1,
                                Maximum = 1,
                                Probability = 0.5f
                            },
                            new ConfigData.PrizeOptions.RandomLoot.LootDefinition
                            {
                                Shortname = "sickle",
                                Minimum = 1,
                                Maximum = 1,
                                Probability = 1f
                            },
                            new ConfigData.PrizeOptions.RandomLoot.LootDefinition
                            {
                                Shortname = "syringe.medical",
                                Minimum = 1,
                                Maximum = 2,
                                Probability = 1f
                            },
                            new ConfigData.PrizeOptions.RandomLoot.LootDefinition
                            {
                                Shortname = "bandage",
                                Minimum = 1,
                                Maximum = 2,
                                Probability = 1f
                            },
                            new ConfigData.PrizeOptions.RandomLoot.LootDefinition
                            {
                                Shortname = "chainsaw",
                                Minimum = 1,
                                Maximum = 1,
                                Probability = 0.5f
                            },
                            new ConfigData.PrizeOptions.RandomLoot.LootDefinition
                            {
                                Shortname = "lowgradefuel",
                                Minimum = 20,
                                Maximum = 40,
                                Probability = 1f
                            },
                            new ConfigData.PrizeOptions.RandomLoot.LootDefinition
                            {
                                Shortname = "chocolate",
                                Minimum = 3,
                                Maximum = 5,
                                Probability = 1f
                            },
                            new ConfigData.PrizeOptions.RandomLoot.LootDefinition
                            {
                                Shortname = "corn",
                                Minimum = 3,
                                Maximum = 5,
                                Probability = 1f
                            },
                            new ConfigData.PrizeOptions.RandomLoot.LootDefinition
                            {
                                Shortname = "burlap.gloves",
                                Minimum = 1,
                                Maximum = 1,
                                Probability = 1f
                            },
                            new ConfigData.PrizeOptions.RandomLoot.LootDefinition
                            {
                                Shortname = "hat.beenie",
                                Minimum = 1,
                                Maximum = 1,
                                Probability = 1f
                            },
                            new ConfigData.PrizeOptions.RandomLoot.LootDefinition
                            {
                                Shortname = "mask.bandana",
                                Minimum = 1,
                                Maximum = 1,
                                Probability = 1f
                            },
                            new ConfigData.PrizeOptions.RandomLoot.LootDefinition
                            {
                                Shortname = "supply.signal",
                                Minimum = 1,
                                Maximum = 1,
                                Probability = 0.4f
                            },
                            new ConfigData.PrizeOptions.RandomLoot.LootDefinition
                            {
                                Shortname = "pistol.python",
                                Minimum = 1,
                                Maximum = 1,
                                Probability = 0.25f,
                                Required = new ConfigData.PrizeOptions.RandomLoot.LootDefinition
                                {
                                    Shortname = "ammo.pistol",
                                    Minimum = 12,
                                    Maximum = 18
                                }
                            },
                            new ConfigData.PrizeOptions.RandomLoot.LootDefinition
                            {
                                Shortname = "rocket.launcher",
                                Minimum = 1,
                                Maximum = 1,
                                Probability = 0.1f,
                                Required = new ConfigData.PrizeOptions.RandomLoot.LootDefinition
                                {
                                    Shortname = "ammo.rocket.basic",
                                    Minimum = 3,
                                    Maximum = 4
                                }
                            }
                        }
                    }
                },
                Timers = new ConfigData.EventTimers
                {
                    Open = 120,
                    Interval = 3600,
                    Completion = 2.5f
                },
                UI = new ConfigData.UISettings
                {
                    Color = "#4C4C4C",
                    Enabled = true,                    
                    Position = new ConfigData.UISettings.CUIPosition
                    {
                        XDimension = 0.295f,
                        XPosition = 0.345f,
                        YDimension = 0.025f,
                        YPosition = 0.1125f,
                    },
                    Opacity = 0.7f,
                    Size = 14
                },
                Version = Version
            } as T;
        }

        protected override void OnConfigurationUpdated(VersionNumber oldVersion)
        {
            ConfigData baseConfigData = GenerateDefaultConfiguration<ConfigData>();

            if (oldVersion < new VersionNumber(0, 3, 0))
                ConfigurationData = baseConfigData;

            if (oldVersion < new VersionNumber(0, 3, 23))
                Configuration.Juggernaut.HurtVehicles = true;

            if (oldVersion < new VersionNumber(0, 3, 36))
            {
                Configuration.Generation = baseConfigData.Generation;
                Configuration.Prizes.Loot = baseConfigData.Prizes.Loot;
            }
        }

        #endregion

        #region Data Management
        private void SaveData()
        {
            restoreData.Save();
            prizeData.Save();
        }

        public class PrizeData
        {
            public Hash<ulong, WinnerData> winnings = new Hash<ulong, WinnerData>();

            public void AddData(BasePlayer player)
            {
                winnings[player.userID] = new WinnerData(player);
            }
            
            public void AddData(BasePlayer player, List<ItemData> items)
            {
                winnings[player.userID] = new WinnerData(items);
            }

            public bool FindDataForPlayer(BasePlayer player, out WinnerData winnerData)
                => winnings.TryGetValue(player.userID, out winnerData);
            
            public void RemoveData(ulong playerId) => winnings.Remove(playerId);
                        
            public class WinnerData
            {
                public List<ItemData> items = new List<ItemData>();

                public int Count => items.Count;

                public WinnerData() { }

                public WinnerData(BasePlayer player)
                {                   
                    items.AddRange(player.inventory.containerBelt.itemList.Select(x => new ItemData(x)));
                    items.AddRange(player.inventory.containerMain.itemList.Select(x => new ItemData(x)));
                    
                    if (!Instance.Configuration.Prizes.RemoveAttire)
                        items.AddRange(player.inventory.containerWear.itemList.Select(x => new ItemData(x)));   
                }

                public WinnerData(List<ItemData> items)
                {
                    this.items.AddRange(items);
                }
            }
        }
        
        #region Prize Claiming
        private void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (!container?.entityOwner || container.entityOwner.IsDestroyed)
                return;
            
            LootHandler lootHandler = container.entityOwner.GetComponent<LootHandler>();
            if (lootHandler)            
                lootHandler.OnItemRemoved(item);            
        }
        
        private void OnLootEntityEnd(BasePlayer player, StorageContainer storageContainer)
        {
            LootHandler lootHandler = storageContainer.GetComponent<LootHandler>();
            if (lootHandler)          
                UnityEngine.Object.Destroy(lootHandler);            
        }
        
        private void OpenClaimContainer(BasePlayer player, PrizeData.WinnerData playerData)
        {
            if (!player || !player.IsConnected || IsUnloading)
                return;

            Subscribe(nameof(OnItemRemovedFromContainer));
            Subscribe(nameof(OnLootEntityEnd));
                
            OpenClaimContainer(player, playerData, OnEndLootingCallback);
        }

        private void OnEndLootingCallback()
        {
            if (LootHandler.ActiveHandlers <= 0)
            {
                Unsubscribe(nameof(OnItemRemovedFromContainer));
                Unsubscribe(nameof(OnLootEntityEnd));
            }
            
            SaveData();
        }
        
        private void OpenClaimContainer(BasePlayer player, PrizeData.WinnerData data, Action onEndLooting)
        {
            const string COFFIN_PREFAB = "assets/prefabs/misc/halloween/coffin/coffinstorage.prefab";
            const string LOOT_PANEL = "generic_resizable";
                
            StorageContainer container = GameManager.server.CreateEntity(COFFIN_PREFAB, player.transform.position + (Vector3.down * 250f)) as StorageContainer;
            container.limitNetworking = true;
            container.enableSaving = false;
            container.inventorySlots = data.Count;
                
            UnityEngine.Object.Destroy(container.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.Destroy(container.GetComponent<GroundWatch>());
            container.Spawn();

            LootHandler lootHandler = container.gameObject.AddComponent<LootHandler>();
                
            player.inventory.loot.Clear();
            player.inventory.loot.SendImmediate();
                
            timer.In(0.05f, ()=>
            {
                lootHandler.Looter = player;
                lootHandler.OnEndLooting = onEndLooting;
                lootHandler.SetData(data);
                    
                player.inventory.loot.PositionChecks = false;
                player.inventory.loot.entitySource = container;
                player.inventory.loot.itemSource = null;
                player.inventory.loot.MarkDirty();
                player.inventory.loot.AddContainer(container.inventory);
                player.inventory.loot.SendImmediate();

                player.ClientRPCPlayer(null, player, "RPC_OpenLootPanel", LOOT_PANEL);
                container.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            });
        }

        private class LootHandler : MonoBehaviour
        {
            public StorageContainer Entity { get; private set; }
            public BasePlayer Looter { get; set; }

            private PrizeData.WinnerData data;
            
            public Action OnEndLooting { get; set; }

            public static int ActiveHandlers = 0;//709
            
            private void Awake()
            {
                ActiveHandlers++;
                
                Entity = GetComponent<StorageContainer>();
                Entity.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
            }

            protected void OnDestroy()
            {
                ActiveHandlers--;
                
                if (Entity && !Entity.IsDestroyed)
                {
                    if (Entity.inventory.itemList.Count > 0)
                    {
                        data.items.Clear();
                        data.items.AddRange(Entity.inventory.itemList.Select(x => new ItemData(x)));
                        ClearContainer();
                    }
                    else prizeData.Data.RemoveData(Looter.userID);
                    Entity.Kill(BaseNetworkable.DestroyMode.None);
                }
                
                OnEndLooting?.Invoke();
            }
            
            private void ClearContainer()
            {               
                for (int i = Entity.inventory.itemList.Count - 1; i >= 0; i--)
                    RemoveItem(Entity.inventory.itemList[i]);                
            }
            
            private bool InsertItem(Item item)
            {
                if (Entity.inventory.itemList.Contains(item))
                    return false;

                if (Entity.inventory.IsFull())
                    return false;

                Entity.inventory.itemList.Add(item);
                item.parent = Entity.inventory;

                if (!Entity.inventory.FindPosition(item))
                    return false;

                Entity.inventory.MarkDirty();
                Entity.inventory.onItemAddedRemoved?.Invoke(item, true);

                return true;
            }
            
            private void RemoveItem(Item item)
            {
                if (!Entity.inventory.itemList.Contains(item))
                    return;

                Entity.inventory.onPreItemRemove?.Invoke(item);

                Entity.inventory.itemList.Remove(item);
                item.parent = null;

                Entity.inventory.MarkDirty();

                Entity.inventory.onItemAddedRemoved?.Invoke(item, false);

                item.Remove(0f);
            }
            
            public void OnItemRemoved(Item item)
            {
                if (Entity.inventory.itemList.Count == 0)
                    Looter.EndLooting();
            }

            public void SetData(PrizeData.WinnerData data)
            {
                this.data = data;

                foreach (ItemData itemData in data.items)
                {
                    Item item = itemData.Create();
                    
                    if (!InsertItem(item))
                        item.Remove(0f);
                    else item.MarkDirty();
                }
            }
        }
        #endregion
        #endregion

        #region Localization
        private string msg(string key, ulong playerId = 0UL) => lang.GetMessage(key, this, playerId != 0UL ? playerId.ToString() : null);

        protected override Dictionary<string, string> Messages { get; } = new Dictionary<string, string>
        {
            ["Notification.Open"] = "The Juggernaut event is now open! To register as a contestant type <color=#ce422b>/juggernaut enter</color>",
            ["Notification.StartsIn"] = "The Juggernaut event starts in <color=#ce422b>{0}</color>",
            ["Notification.NoContestants"] = "The Juggernaut event has been cancelled. No contestants registered...",
            ["Notification.NotEnoughContestants"] = "The Juggernaut event has been cancelled. Not enough contestants registered. The event requires <color=#ce422b>{0}%</color> of online players to register",
            ["Notification.EventStarted"] = "The Juggernaut event has started! Hunt down the Juggernaut to win!",
            ["Notification.JuggernautDead"] = "The Juggernaut has been killed!",
            ["Notification.JuggernautSuicide"] = "The Juggernaut has killed themselves...",
            ["Notification.JuggernautKilledBy"] = "The Juggernaut has been killed by <color=#ce422b>{0}</color>!",
            ["Notification.FriendlyFire"] = "You can not damage the juggenaut as you were friends",
            ["Notification.OnTrade"] = "You can not trade when you are the juggernaut...",
            ["Notification.OnRemove"] = "You can not remove when you are the juggernaut...",
            ["Notification.OnTP"] = "You can not teleport when you are the juggernaut...",
            ["Notification.OnShop"] = "You can not shop when you are the juggernaut...",
            ["Notification.OnKit"] = "You can not claim a kit when you are the juggernaut...",
            ["Notification.BlacklistedCommand"] = "You can not use that command when you are the juggernaut...",
            ["Notification.Position"] = "The Juggernaut was spotted near <color=#ce422b>{0}</color>!",
            ["Notification.AdminCancelled"] = "The Juggernaut event has been cancelled by admin",
            ["Notification.OnWin.Global"] = "The Juggernaut has won the event!",
            ["Notification.OnWin.Prize"] = "To claim your prize type <color=#ce422b>/juggernaut claim</color>! Make sure you have a empty inventory when you claim your prize",
            ["Notification.TimeExpired"] = "The Juggernaut run out of time to make it to their destination!",
            ["Notification.Disconnected"] = "The Juggernaut disconnected...",
            ["Notification.PlayerJoined"] = "<color=#ce422b>{0}</color> has entered the draw to be the Juggernaut\n(<color=#ce422b>{1} player(s) in draw</color>)",
            ["Notification.PlayerLeft"] = "<color=#ce422b>{0}</color> has left the draw to be the Juggernaut\n(<color=#ce422b>{1} player(s) in draw</color>)",
            ["Notification.JuggernautSelected"] = "<color=#ce422b>{0}</color> has been selected to be the Juggernaut!",
            ["Notification.VehicleDamage"] = "This vehicle is taking damage because you are riding it!",
            ["Chat.Error.NoPermission"] = "You do not have permission to use this command",
            ["Chat.Error.NoEventInProgress"] = "There is currently no event in progress",
            ["Chat.Error.EventInProgress"] = "You can not open a event when one is already in progress",
            ["Chat.Error.NoEventOpen"] = "You must open a event before you can start one",
            ["Chat.Error.NoContestants"] = "Unable to start event. No contestants have registered to play",
            ["Chat.Enter.AlreadyAContestant"] = "You are already registered as a contestant",
            ["Chat.Enter.Added"] = "You have registered as a contestant",
            ["Chat.Leave.NotAContestant"] = "You are not registered as a contestant",
            ["Chat.Leave.Removed"] = "You are not registered as a contestant",
            ["Chat.Claim.NoData"] = "You do not have any prize data saved",
            ["Chat.Claim.Success"] = "You have successfully claimed your prize!",
            ["Chat.Help.Enter"] = "<color=#ce422b>/juggernaut enter</color> - Register yourself as a contestant",
            ["Chat.Help.Leave"] = "<color=#ce422b>/juggernaut leave</color> - Revoke your registration as a contestant",
            ["Chat.Help.Claim"] = "<color=#ce422b>/juggernaut claim</color> - Claim any outstanding rewards",
            ["Chat.Help.Open"] = "<color=#ce422b>/juggernaut open</color> - Force open an event",
            ["Chat.Help.Start"] = "<color=#ce422b>/juggernaut start</color> - Force start an event",
            ["Chat.Help.Cancel"] = "<color=#ce422b>/juggernaut cancel</color> - Force finish a event",
            ["Chat.Open.Success"] = "You have force opened the event",
            ["Chat.Start.Success"] = "You have force started the event",
            ["Chat.Cancel.Success"] = "You have force cancelled the event",
            ["UI.OpenMessage"] = "The Juggernaut event starts in <color=#ce422b>{0}</color>",
            ["UI.ProgressMessage"] = "The Juggernaut has <color=#ce422b>{0}</color> to make it to the destination!",
            ["Tip.Compass"] = "Your destination has been marked on your map and compass.\nMake it to the marker to win the event!",
        };
        #endregion
    }
}
