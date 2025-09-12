using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    [Info("RocketTurrets", "k1lly0u", "2.1.1")]
    [Description("Allow users to covert auto turrets to fire rockets")]
    class RocketTurrets : RustPlugin
    {
        #region Fields
        private StoredData storedData;
        private DynamicConfigFile data;

        [PluginReference] Plugin Clans, Friends;

        private QueueUpdater queueUpdater;

        private static Permission Permission;

        private bool hasLoaded;
        private static bool isUnloading;
        private static bool isCallingTargetHook;

        private const string ROCKET_BASIC_ITEM = "ammo.rocket.basic";
        private const string ROCKET_HV_ITEM = "ammo.rocket.hv";
        private const string ROCKET_FIRE_ITEM = "ammo.rocket.fire";
        private const string ROCKET_LAUNCHER_ITEM = "rocket.launcher";
        private const string CAMERA_ITEM = "cctv.camera";
        private const string COMPUTER_ITEM = "targeting.computer";

        private const string ROCKET_BASIC_PREFAB = "assets/prefabs/ammo/rocket/rocket_basic.prefab";
        private const string ROCKET_HV_PREFAB = "assets/prefabs/ammo/rocket/rocket_hv.prefab";
        private const string ROCKET_FIRE_PREFAB = "assets/prefabs/ammo/rocket/rocket_fire.prefab";
        private const string FIREBALL_PREFAB = "assets/bundled/prefabs/oilfireballsmall.prefab";

        private const string ROCKET_FIRE_EFFECT = "assets/prefabs/npc/patrol helicopter/effects/rocket_fire.prefab";

        private const string WEAPON_SOCKET_BONE = "WeaponAttachmentPoint";

        private const string BASIC_PERMISSION = "rocketturrets.rocket";
        private const string AA_PERMISSION = "rocketturrets.antiair";
        private const string JAVELIN_PERMISSION = "rocketturrets.javelin";
        private const string NOLIMIT_PERMISSION = "rocketturrets.unlimited";
        private const string NOAMMO_PERMISSION = "rocketturrets.unlimitedammo";
        
        private static List<PatrolHelicopter> _patrolHelicopters = new List<PatrolHelicopter>();

        #endregion

        #region Oxide Hooks
        private void Loaded()
        {
            isUnloading = false;
            Permission = permission;

            lang.RegisterMessages(Messages, this);

            permission.RegisterPermission(BASIC_PERMISSION, this);
            permission.RegisterPermission(AA_PERMISSION, this);
            permission.RegisterPermission(JAVELIN_PERMISSION, this);
            permission.RegisterPermission(NOAMMO_PERMISSION, this);
            permission.RegisterPermission(NOLIMIT_PERMISSION, this);

            foreach (string key in configData.VIPLimits.Keys)
                permission.RegisterPermission(key, this);

            data = Interface.Oxide.DataFileSystem.GetFile("rocketturrets_data");

            //Physics.IgnoreLayerCollision(18, 13, false);

            LoadData();
        }

        private void OnServerInitialized()
        {
            _patrolHelicopters.Clear();
            IEnumerable<BaseNetworkable> helicopters = BaseNetworkable.serverEntities.Where(x => x is PatrolHelicopter);
            foreach (BaseNetworkable baseNetworkable in helicopters)
            {
                if (baseNetworkable is PatrolHelicopter helicopter)
                    _patrolHelicopters.Add(helicopter);
            }
            
            timer.In(3f, RestoreTurrets);
        }

        private void OnNewSave(string str) => storedData.launchers.Clear();

        private void OnServerSave()
        {
            if (!Interface.Oxide.IsShuttingDown)
            {
                if (hasLoaded)
                {
                    for (int i = RocketTurret.allTurrets.Count - 1; i >= 0; i--)
                        RocketTurret.allTurrets[i]?.PrepareForGameSave();

                    SaveRestore.Instance.StartCoroutine(RestoreOnSaveFinished());
                }
            }
            SaveData();
        }
        
        private void OnEntitySpawned(PatrolHelicopter helicopter)
        {
            if (!helicopter || _patrolHelicopters.Contains(helicopter))
                return;

            _patrolHelicopters.Add(helicopter);
        }
        
        private void OnEntityKill(PatrolHelicopter helicopter)
        {
            if (!helicopter || !_patrolHelicopters.Contains(helicopter))
                return;

            _patrolHelicopters.Remove(helicopter);
        }

        private IEnumerator RestoreOnSaveFinished()
        {
            while (SaveRestore.IsSaving)
                yield return null;

            yield return CoroutineEx.waitForEndOfFrame;
            yield return CoroutineEx.waitForEndOfFrame;

            for (int i = RocketTurret.allTurrets.Count - 1; i >= 0; i--)
                RocketTurret.allTurrets[i]?.RestoreAfterGameSave();
        }

        private void CanPickupEntity(AutoTurret turret, BasePlayer player)
        {
            if (configData.Settings.ReturnOnPickup == 0)
                return;
            
            RocketTurret rocketTurret = turret.GetComponent<RocketTurret>();
            if (!rocketTurret) 
                return;
            
            ReturnResources(player, rocketTurret.type, configData.Settings.ReturnOnPickup);
            UnityEngine.Object.Destroy(rocketTurret);
        }

        private object OnTurretTarget(AutoTurret turret, BaseCombatEntity baseCombatEntity)
        {
            if (isCallingTargetHook || !turret || !baseCombatEntity)
                return null;

            RocketTurret rocketTurret = turret.GetComponent<RocketTurret>();
            if (!rocketTurret)
                return null;

            if (Vector3.Distance(baseCombatEntity.transform.position, rocketTurret.transform.position) > rocketTurret.Config.MaxTargetingDistance)
                return true;

            return null;
        }

        private static object CallTargetingHook(AutoTurret turret, BaseCombatEntity baseCombatEntity)
        {
            isCallingTargetHook = true;
            object obj = Interface.Call("OnTurretTarget", turret, baseCombatEntity);
            isCallingTargetHook = false;
            return obj;
        }

        private void Unload()
        {
            if (queueUpdater)
                UnityEngine.Object.Destroy(queueUpdater.gameObject);

            //Physics.IgnoreLayerCollision(18, 13, true);

            SaveData();

            isUnloading = true;

            for (int i = RocketTurret.allTurrets.Count - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(RocketTurret.allTurrets[i]);

            RocketTurret.allTurrets.Clear();

            RocketTurret[] rocketTurrets = UnityEngine.Object.FindObjectsOfType<RocketTurret>();
            for (int i = 0; i < rocketTurrets.Length; i++)
                UnityEngine.Object.Destroy(rocketTurrets[i]);

            Permission = null;
            configData = null;
        }
        #endregion

        #region Functions
        private void RestoreTurrets()
        {
            if (storedData.launchers.Count > 0)
            {
                List<BaseNetworkable> entities = BaseNetworkable.serverEntities.Where(x => x is AutoTurret).ToList();
                foreach (BaseNetworkable entity in entities)
                {
                    if (storedData.launchers.TryGetValue(entity.net.ID.Value, out StoredData.Turret t))
                    {
                        RocketTurret.Load(entity as AutoTurret, t);
                    }
                }
            }

            queueUpdater = QueueUpdater.Create();

            hasLoaded = true;
        }

        private bool ParseType<T>(string type, out T value)
        {
            value = default(T);
            try
            {
                value = (T)Enum.Parse(typeof(T), type, true);
                if (Enum.IsDefined(typeof(T), value))
                    return true;
            }
            catch
            {                
                return false;
            }
            return false;
        }

        private T FindEntityFromRay<T>(BasePlayer player) where T : Component
        {
            if (!Physics.Raycast(player.eyes.HeadRay(), out RaycastHit hit, 3f))
                return null;

            return hit.collider?.GetComponentInParent<T>();            
        }

        private int CountTurrets(ulong userId, TurretType type)
        {
            int count = 0;
            for (int i = 0; i < RocketTurret.allTurrets.Count; i++)
            {
                RocketTurret rocketTurret = RocketTurret.allTurrets[i];
                if (rocketTurret.Turret.OwnerID == userId && rocketTurret.type == type)
                    count++;
            }
            return count;
        }

        private int CountTurretsForBuilding(AutoTurret autoTurret)
        {
            BuildingManager.Building building = autoTurret.GetBuilding();
                       
            int count = 0;
            foreach (DecayEntity decayEntity in building.decayEntities)
            {
                if (!(decayEntity is AutoTurret))
                    continue;

                if (decayEntity.GetComponent<RocketTurret>())
                    count++;                
            }
            return count;
        }

        private static List<ConfigData.TurretConfig.ItemCost> GetTurretCosts(TurretType type) => type == TurretType.AA ? configData.AntiAir.Cost : type == TurretType.Basic ? configData.Basic.Cost : configData.Javelin.Cost;

        private bool HasResources(BasePlayer player, TurretType type)
        {
            List<ConfigData.TurretConfig.ItemCost> costs = GetTurretCosts(type);

            for (int i = 0; i < costs.Count; i++)
            {
                ConfigData.TurretConfig.ItemCost cost = costs[i];
                if (player.inventory.GetAmount(ItemManager.itemDictionaryByName[cost.Shortname].itemid) < cost.Amount)
                    return false;
            }
            return true;
        }

        private void TakeResources(BasePlayer player, TurretType type)
        {
            List<ConfigData.TurretConfig.ItemCost> costs = GetTurretCosts(type);

            for (int i = 0; i < costs.Count; i++)
            {
                ConfigData.TurretConfig.ItemCost cost = costs[i];
                player.inventory.Take(null, ItemManager.itemDictionaryByName[cost.Shortname].itemid, cost.Amount);
            }
        }

        private void ReturnResources(BasePlayer player, TurretType type, float percentage)
        {
            List<ConfigData.TurretConfig.ItemCost> costs = GetTurretCosts(type);
            for (int i = 0; i < costs.Count; i++)
            {
                ConfigData.TurretConfig.ItemCost cost = costs[i];
                int amount = Mathf.RoundToInt(cost.Amount * (percentage / 100));
                if (amount == 0)
                    continue;

                Item item = ItemManager.CreateByName(cost.Shortname, amount);
                player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
            }
        }

        private static void DropResources(AutoTurret turret, TurretType type, float percentage)
        {
            List<ConfigData.TurretConfig.ItemCost> costs = GetTurretCosts(type);
            for (int i = 0; i < costs.Count; i++)
            {
                ConfigData.TurretConfig.ItemCost cost = costs[i];
                int amount = Mathf.CeilToInt(cost.Amount * (percentage / 100f));
                if (amount == 0)
                    continue;

                if (cost.Shortname == ROCKET_LAUNCHER_ITEM)
                    continue;

                Vector3 random = Random.onUnitSphere;
                random.y = Mathf.Abs(random.y);

                Item item = ItemManager.CreateByName(cost.Shortname, amount);
                item.Drop(turret.transform.position + Vector3.up, (Vector3.up + random) * 2f);
            }
        }

        private int GetTurretLimit(string playerId, TurretType type)
        {
            int count = -1;

            foreach (KeyValuePair<string, Dictionary<TurretType, int>> kvp in configData.VIPLimits)
            {
                if (permission.UserHasPermission(playerId, kvp.Key))
                {
                    int amount = kvp.Value[type];
                    if (amount > count)
                        count = amount;
                }
            }

            if (count == -1)
                count = type == TurretType.Basic ? configData.Basic.MaxPerPlayer : type == TurretType.AA ? configData.AntiAir.MaxPerPlayer : configData.Javelin.MaxPerPlayer;

            return count;
        }
        #endregion

        #region API
        private void TryFireRocket(AutoTurret turret)
        {
            RocketTurret rocketTurret = turret.GetComponent<RocketTurret>();
            if (rocketTurret)
            {
                if (Time.time >= rocketTurret.nextShotTime && turret.targetVisible)
                {
                    if (rocketTurret.HasAmmo())
                    {
                        Vector3 muzzlePos = turret.eyePos.position + (turret.eyePos.rotation * Vector3.forward * 10f);

                        if (rocketTurret.FireStandardRocket(muzzlePos))
                            rocketTurret.OnRocketFired();
                    }
                }
            }            
        }

        private bool IsRocketTurret(AutoTurret turret) => turret.GetComponent<RocketTurret>();

        private void ToggleTurretAutomation(AutoTurret turret, bool isOn)
        {
            RocketTurret rocketTurret = turret.GetComponent<RocketTurret>();
            if (rocketTurret)                            
                turret.SetIsOnline(isOn);            
        }
        #endregion

        #region Components  
        private class QueueUpdater : MonoBehaviour
        {
            internal static QueueUpdater Create() => new GameObject("RocketTurret.QueueUpdater").AddComponent<QueueUpdater>();

            private void Update()
            {
                RocketTurret.updateTurretScanQueue.RunQueue(0.5);
            }
        }

        private class UpdateRocketTurretScanQueue : ObjectWorkQueue<RocketTurret>
        {       
            #if (OXIDE_PUBLICIZED)
            public override void RunJob(RocketTurret turret)
            #else
            protected override void RunJob(RocketTurret turret)
            #endif
            {
                if (!ShouldAdd(turret))                
                    return;

                turret.TargetScan();
            }

            #if (OXIDE_PUBLICIZED)
            public override bool ShouldAdd(RocketTurret turret)
            #else
            protected override bool ShouldAdd(RocketTurret turret)
            #endif
            {
                if (!base.ShouldAdd(turret))                
                    return false;
                
                return turret && turret.Turret && turret.Turret.IsValid();
            }
        }

        #region Turrets
        private class RocketTurret : MonoBehaviour
        {
            internal static List<RocketTurret> allTurrets = new List<RocketTurret>();

            internal static UpdateRocketTurretScanQueue updateTurretScanQueue;

            static RocketTurret()
            {
                updateTurretScanQueue = new UpdateRocketTurretScanQueue();
            }

            internal AutoTurret Turret { get; private set; }

            internal BaseLauncher BaseLauncher { get; private set; }

            internal Item LauncherItem { get; private set; }

            internal TurretType type;

            internal Transform tr;

            private List<BaseEntity> attachments;

            private Item lastAmmoItem;

            private float targetAcquiredTime;

            internal float nextShotTime = Time.time;

            private bool dropResources;

            public float nextScanTime;
            
            private RealTimeSinceEx timeSinceLastServerTick;

            public virtual ConfigData.TurretConfig Config => configData.Basic;

            public static void Load(AutoTurret autoTurret, StoredData.Turret storedTurret)
            {
                if (autoTurret.GetComponent<RocketTurret>())
                    return;

                switch (storedTurret.type)
                {
                    case TurretType.Basic:
                        RocketTurret rocketTurret = autoTurret.gameObject.AddComponent<RocketTurret>();
                        Interface.Oxide.NextTick(() => rocketTurret.OnLoad(storedTurret));
                        break;
                    case TurretType.AA:
                        AATurret aaTurret = autoTurret.gameObject.AddComponent<AATurret>();
                        Interface.Oxide.NextTick(() => aaTurret.OnLoad(storedTurret));
                        break;
                    case TurretType.Javelin:
                        JavelinTurret javelinTurret = autoTurret.gameObject.AddComponent<JavelinTurret>();
                        Interface.Oxide.NextTick(() => javelinTurret.OnLoad(storedTurret));
                        break;
                }
            }

            protected virtual void Awake()
            {
                allTurrets.Add(this);

                Turret = GetComponent<AutoTurret>();

                type = TurretType.Basic;

                tr = Turret.transform;

                attachments = Pool.Get<List<BaseEntity>>();

                BuildCosmetics();

                Turret.CancelInvoke(Turret.ServerTick);
                Turret.CancelInvoke(Turret.ScheduleForTargetScan);

                AutoTurret.updateAutoTurretScanQueue.Remove(Turret);

                InvokeHandler.InvokeRepeating(this, ServerTick, Random.Range(0f, 1f), 0.015f); //5846
                InvokeHandler.InvokeRandomized(this, ScheduleForTargetScan, Random.Range(0f, 1f), Turret.TargetScanRate(), 0.2f);

                Turret.targetTrigger.GetComponent<SphereCollider>().radius = Config.MaxTargetingDistance;
                
                Turret.SetTarget(null);
            }

            private void Start()
            {
                AutoTurret.updateAutoTurretScanQueue.Remove(Turret);
            }

            protected virtual void OnDestroy()
            {                
                InvokeHandler.CancelInvoke(this, ServerTick);
                InvokeHandler.CancelInvoke(this, ScheduleForTargetScan);

                RemoveCosmetics();
                               
                if (Turret && !Turret.IsDestroyed)
                {
                    if (dropResources)
                        DropResources(Turret, type, configData.Settings.ReturnOnRemove);

                    Turret.inventory.canAcceptItem -= CanAcceptItem;
                    Turret.inventory.onItemAddedRemoved -= OnItemAddedOrRemoved;

                    if (LauncherItem != null)
                    {
                        LauncherItem.RemoveFromWorld();
                        LauncherItem.RemoveFromContainer();
                        LauncherItem.Remove();
                    }

                    Turret.inventory.onlyAllowedItems = null;

                    Turret.inventory.canAcceptItem += Turret.CanAcceptItem;                    
                    Turret.inventory.onItemAddedRemoved += Turret.OnItemAddedOrRemoved;

                    Turret.AttachedWeapon = null;

                    Turret.Invoke(Turret.UpdateAttachedWeapon, 0.5f);

                    if (Turret.IsOnline())
                    {
                        Turret.InvokeRepeating(Turret.ServerTick, Random.Range(0f, 1f), 0.015f);
                        Turret.InvokeRandomized(Turret.ScheduleForTargetScan, Random.Range(0f, 1f), Turret.TargetScanRate(), 0.2f);
                    }
                    Turret.targetTrigger.GetComponent<SphereCollider>().radius = Turret.sightRange;                    
                }
                                
                Pool.FreeUnmanaged(ref attachments);
                allTurrets.Remove(this);
            }

            public void OnLoad(StoredData.Turret storedTurret)
            {
                if (!BaseLauncher)
                {
                    Debug.LogError("No launcher");
                    return;
                }

                if (!string.IsNullOrEmpty(storedTurret.loadedAmmo) && BaseLauncher.primaryMagazine.ammoType.shortname != storedTurret.loadedAmmo)                
                    BaseLauncher.primaryMagazine.ammoType = ItemManager.FindItemDefinition(storedTurret.loadedAmmo);

                BaseLauncher.primaryMagazine.contents = storedTurret.loadedCapacity;
            }

            #region Save Hack
            public void PrepareForGameSave()
            {
                if (LauncherItem != null)
                    LauncherItem.removeTime = 1f;
            }

            public void RestoreAfterGameSave()
            {
                if (LauncherItem != null)
                    LauncherItem.removeTime = 0f;
            }
            #endregion

            #region Cosmetics           
            protected virtual void BuildCosmetics()
            {
                Item currentWeapon = Turret.inventory.GetSlot(0);
                bool hasLauncher = false;
                if (currentWeapon != null)
                {
                    if (currentWeapon.info.shortname != ROCKET_LAUNCHER_ITEM)
                    {
                        currentWeapon.RemoveFromContainer();
                        currentWeapon.Drop(Turret.transform.position + Vector3.up, Vector3.up * 3f);
                    }
                    else
                    {
                        BaseLauncher = currentWeapon.GetHeldEntity() as BaseLauncher;
                        hasLauncher = true;
                    }
                }

                Turret.inventory.canAcceptItem -= Turret.CanAcceptItem;
                Turret.inventory.canAcceptItem += CanAcceptItem;
                Turret.inventory.onItemAddedRemoved -= Turret.OnItemAddedOrRemoved;
                Turret.inventory.onItemAddedRemoved += OnItemAddedOrRemoved;

                List<ItemDefinition> allowedRockets = Pool.Get<List<ItemDefinition>>();
                if (Config.AllowedRockets.Contains(ROCKET_BASIC_ITEM))
                    allowedRockets.Add(BasicRocketDefinition);
                
                if (Config.AllowedRockets.Contains(ROCKET_FIRE_ITEM))
                    allowedRockets.Add(IncendiaryRocketDefinition);

                if (Config.AllowedRockets.Contains(ROCKET_HV_ITEM))
                    allowedRockets.Add(HvRocketDefinition);

                Turret.inventory.onlyAllowedItems = allowedRockets.ToArray();
                Pool.FreeUnmanaged(ref allowedRockets);

                if (!hasLauncher)
                {
                    LauncherItem = ItemManager.CreateByName(ROCKET_LAUNCHER_ITEM, 1, Config.LauncherSkin);
                    MoveItemToInventory(LauncherItem);

                    BaseLauncher = LauncherItem.GetHeldEntity() as BaseLauncher;
                }
                else
                {
                    LauncherItem = currentWeapon;

                    if (!Turret.IsInvoking(UpdateAttachedWeapon))
                        Turret.Invoke(UpdateAttachedWeapon, 0.5f);
                }

                AttachToTurret(ItemManager.CreateByName(ROCKET_BASIC_ITEM), new Vector3(0.2f, 0f, -0.1f), new Vector3(0, 90, 0));
                AttachToTurret(ItemManager.CreateByName(ROCKET_BASIC_ITEM), new Vector3(-0.2f, 0f, -0.1f), new Vector3(0, 90, 0));
            }
                        
            protected void AttachToTurret(Item item, Vector3 localPosition, Vector3 localEuler, string bone = WEAPON_SOCKET_BONE)
            {
                BaseEntity worldEntity = GameManager.server.CreateEntity("assets/prefabs/misc/burlap sack/generic_world.prefab", tr.position, tr.rotation);
                
                WorldItem worldItem = worldEntity as WorldItem;

                worldItem.InitializeItem(item);
                worldItem.allowPickup = false;

                worldEntity.enableSaving = false;
                worldEntity.Spawn();

                worldItem.CancelInvoke((worldItem as DroppedItem).IdleDestroy);
                item.SetWorldEntity(worldEntity);

                Rigidbody rb = worldEntity.GetComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                worldEntity.SetParent(Turret, bone);
                worldEntity.transform.localPosition = localPosition;
                worldEntity.transform.localRotation = Quaternion.Euler(localEuler);

                attachments.Add(worldEntity);
            }            

            private void RemoveCosmetics()
            {                
                for (int i = 0; i < attachments?.Count; i++)
                {
                    BaseEntity worldEntity = attachments[i];
                    if (worldEntity && !worldEntity.IsDestroyed)
                    {
                        (worldEntity as WorldItem).DestroyItem();
                        worldEntity.Kill();
                    }
                }
            }
            #endregion

            #region Item Management
            private void MoveItemToInventory(Item item)
            {                
                item.RemoveFromContainer();
                item.RemoveFromWorld();
                item.position = 0;
                item.SetParent(Turret.inventory);

                Turret.inventory.MarkDirty();
                item.MarkDirty();

                if (!Turret.IsInvoking(UpdateAttachedWeapon))
                    Turret.Invoke(UpdateAttachedWeapon, 0.5f);
            }

            private void OnItemAddedOrRemoved(Item item, bool added)
            {
                if (isUnloading)                
                    return;

                if (!added && item != null && item.info.shortname == ROCKET_LAUNCHER_ITEM)
                {
                    dropResources = true;
                    Destroy(this, 0.5f);
                }
            }

            private bool CanAcceptItem(Item item, int targetSlot)
            {
                Item slot = Turret.inventory.GetSlot(0);
                if (item.info.shortname == ROCKET_LAUNCHER_ITEM && targetSlot == 0)
                    return true;

                if (item.info.category != ItemCategory.Ammunition || slot == null || !Turret.GetAttachedWeapon() || targetSlot == 0)
                    return false;

                return true;
            }

            private void UpdateAttachedWeapon()
            {
                Item slot = Turret.inventory.GetSlot(0);
                BaseLauncher baseLauncher = null;

                if (slot != null && slot.info.category == ItemCategory.Weapon)
                {
                    BaseEntity heldEntity = slot.GetHeldEntity();
                    if (heldEntity)
                    {
                        baseLauncher = heldEntity.GetComponent<BaseLauncher>();                        
                    }
                }

                Turret.SetFlag(BaseEntity.Flags.Reserved3, baseLauncher);
                if (!baseLauncher)
                {
                    if (Turret.AttachedWeapon)
                    {
                        Turret.AttachedWeapon.SetGenericVisible(false);
                        Turret.AttachedWeapon.SetLightsOn(false);
                    }
                    Turret.AttachedWeapon = null;
                    return;
                }

                baseLauncher.SetLightsOn(true);
                baseLauncher.limitNetworking = false;
                baseLauncher.SetFlag(BaseEntity.Flags.Disabled, false);

                baseLauncher.SetParent(Turret, WEAPON_SOCKET_BONE);

                baseLauncher.transform.localPosition = new Vector3(-0.15f, 0.1f, 0f);
                baseLauncher.transform.localRotation = Quaternion.Euler(180f, 95f, -52f);

                baseLauncher.SetGenericVisible(true);

                BaseLauncher = baseLauncher;
                Turret.AttachedWeapon = baseLauncher;
                Turret.totalAmmoDirty = true;
                Turret.Reload();
                Turret.UpdateTotalAmmo();

                baseLauncher.SendNetworkUpdateImmediate();
                Turret.SendNetworkUpdateImmediate();
            }

            #endregion

            #region Think
            private void ServerTick()
            {                
                if (!Turret || Turret.IsDestroyed)                
                    return;

                float timeSince = (float) timeSinceLastServerTick;
                timeSinceLastServerTick = 0;
                
                if (!Turret.IsOnline())                
                    Turret.OfflineTick();                
                else if (!Turret.HasTarget())                
                    Turret.IdleTick(timeSince);                
                else TargetTick();
                
                Turret.UpdateFacingToTarget(timeSince);
                if (Turret.totalAmmoDirty && Time.time > Turret.nextAmmoCheckTime)
                {
                    Turret.UpdateTotalAmmo();
                    Turret.totalAmmoDirty = false;
                    Turret.nextAmmoCheckTime = Time.time + 0.5f;
                }
            }

            private void ScheduleForTargetScan()
            {
                updateTurretScanQueue.Add(this);
            }

            internal virtual void TargetScan()
            {
                if (Turret.IsOffline())                
                    return;

                Vector3 muzzlePos = Turret.eyePos.position + (Turret.eyePos.rotation * Vector3.forward * 0.5f);
                
                if (Turret.HasTarget())
                {
                    if (Vector3.Distance(muzzlePos, Turret.target.transform.position) < Config.MinTargetingDistance || ShouldForgetTarget(Turret.target))
                        Turret.SetTarget(null);
                    return;
                }

                if (Turret.targetTrigger.entityContents != null)
                { 
                    foreach (BaseEntity baseEntity in Turret.targetTrigger.entityContents)
                    {
                        if (!baseEntity) 
                            continue;
                        
                        BaseCombatEntity component = baseEntity as BaseCombatEntity;
                        if (!component || !component.IsAlive() || !Turret.InFiringArc(component) || !Turret.ObjectVisible(component)) 
                            continue;
                        
                        if (!CanTargetEntity(component))
                            continue;

                        if (Vector3.Distance(muzzlePos, component.transform.position) < Config.MinTargetingDistance)
                            continue;

                        if (Turret.PeacekeeperMode())
                        {
                            if (!Turret.IsEntityHostile(component))
                            {
                                continue;
                            }
                            if (!Turret.target)
                            {
                                nextShotTime = Time.time + 1f;
                            }
                        }
                                
                        SetTarget(component);
                        break;
                    }
                }
            }

            protected virtual bool ShouldForgetTarget(BaseCombatEntity baseCombatEntity)
            {
                return false;
            }

            protected void SetTarget(BaseCombatEntity component)
            {              
                if (component != Turret.target)
                    targetAcquiredTime = Time.realtimeSinceStartup;

                isCallingTargetHook = true;
                Turret.SetTarget(component);
                isCallingTargetHook = false;
            }

            protected virtual bool CanTargetEntity(BaseCombatEntity component)
            {
            //    if (component is AutoTurret or RidableHorse2)
                    return false;

                BasePlayer player = component as BasePlayer;
                if (player)
                {
                    if (!Config.TargetNPCs && player.IsNpc)
                        return false;

                    if (Turret.IsAuthed(player))
                        return false;
                }

                BaseVehicle vehicle = component as BaseVehicle;
                if (vehicle && vehicle.mountPoints != null)
                {
                    foreach (BaseVehicle.MountPointInfo vehicleMountPoint in vehicle.mountPoints)
                    {
                        if (vehicleMountPoint == null || !vehicleMountPoint.mountable)
                            continue;
                        
                        player = vehicleMountPoint.mountable.GetMounted();
                        if (player && Turret.IsAuthed(player))
                            return false;
                    }
                }
                
                object obj = CallTargetingHook(Turret, component);
                if (obj is bool b)
                    return b;
                
                return true;
            }

            private void TargetTick()
            {
                if (Time.realtimeSinceStartup >= Turret.nextVisCheck)
                {
                    Turret.nextVisCheck = Time.realtimeSinceStartup + Random.Range(0.2f, 0.3f);
                    Turret.targetVisible = Turret.ObjectVisible(Turret.target);
                    if (Turret.targetVisible)
                    {
                        Turret.lastTargetSeenTime = Time.realtimeSinceStartup;
                    }
                }

                if (Time.time >= nextShotTime && Turret.targetVisible)
                {
                    if (!HasAmmo())                    
                        nextShotTime = Time.time + Config.FireRate;                    
                    else
                    {
                        if (Time.realtimeSinceStartup < targetAcquiredTime + Config.LockDuration)
                            return;

                        if (FireRocket(Turret.target.transform.position, Turret.target))
                            OnRocketFired();
                    }
                }
                if (Turret.target.IsDead() || Time.realtimeSinceStartup - Turret.lastTargetSeenTime > 3f || Vector3.Distance(transform.position, Turret.target.transform.position) > Turret.sightRange || 
                    (Turret.PeacekeeperMode() && !Turret.IsEntityHostile(Turret.target)))
                {
                    SetTarget(null);
                }
            }

            internal bool HasAmmo()
            {
                if (!Config.RequiresAmmo || Permission.UserHasPermission(Turret.OwnerID.ToString(), NOAMMO_PERMISSION))
                    return true;

                if (lastAmmoItem == null || lastAmmoItem.amount == 0)
                {
                    for (int i = 0; i < Turret.inventory.itemList.Count; i++)
                    {
                        Item item = Turret.inventory.itemList[i];
                        if (item.info.shortname is ROCKET_BASIC_ITEM or ROCKET_FIRE_ITEM or ROCKET_HV_ITEM && item.amount > 0)
                        {
                            BaseLauncher.primaryMagazine.ammoType = item.info;
                            lastAmmoItem = item;

                            return true;
                        }
                    }
                    return false;
                }

                return true;
            }
            #endregion

            #region Shoot

            protected virtual bool FireRocket(Vector3 targetPos, BaseCombatEntity target = null)
            {
                if (Turret.IsOffline())                
                    return false;

                Vector3 muzzlePos = Turret.eyePos.position + (Turret.eyePos.rotation * Vector3.forward * 0.5f);
                
                BaseEntity rocket = GameManager.server.CreateEntity(GetPrefabFromItem(), muzzlePos, Quaternion.Euler(targetPos - muzzlePos));
                rocket.enableSaving = false;
                rocket.Spawn();

                Effect.server.Run(ROCKET_FIRE_EFFECT, rocket.transform.position);

                if (Config.RocketTrail)
                    AttachFireball(rocket);

                rocket.creatorEntity = (BasePlayer.FindAwakeOrSleeping(Turret.OwnerID.ToString()) as BaseEntity) ?? Turret;

                ConfigData.BasicTurret config = Config as ConfigData.BasicTurret;
                if (config.SeekTarget)
                {
                    HeatSeekerRocket heatSeeker = rocket.gameObject.AddComponent<HeatSeekerRocket>();
                    heatSeeker.Initialize(target, targetPos, config.RocketSpeed, true, config.ProximityDetonation, config.DamageMultiplier);
                }
                else
                {
                    StandardRocket standardRocket = rocket.gameObject.AddComponent<StandardRocket>();
                    standardRocket.Initialize(target, targetPos, config.RocketSpeed, false, config.ProximityDetonation, config.DamageMultiplier);
                }
                return true;
            }

            protected void AttachFireball(BaseEntity parent)
            {
                FireBall fireball = GameManager.server.CreateEntity(FIREBALL_PREFAB, parent.transform.position) as FireBall;
                fireball.enableSaving = false;
                fireball.Spawn();

                Rigidbody rb = fireball.GetComponent<Rigidbody>();
                rb.useGravity = false;
                rb.isKinematic = true;

                fireball.SetParent(parent);
                fireball.transform.localPosition = Vector3.zero;
            }

            internal bool FireStandardRocket(Vector3 targetPos)
            {
                if (Turret.IsOffline())
                    return false;

                Vector3 muzzlePos = Turret.eyePos.position + (Turret.eyePos.rotation * Vector3.forward * 0.5f);
                
                BaseEntity rocket = GameManager.server.CreateEntity(GetPrefabFromItem(), muzzlePos, Quaternion.Euler(targetPos - muzzlePos));
                rocket.enableSaving = false;
                rocket.Spawn();

                Effect.server.Run(ROCKET_FIRE_EFFECT, rocket.transform.position);

                rocket.creatorEntity = (BasePlayer.FindAwakeOrSleeping(Turret.OwnerID.ToString()) as BaseEntity) ?? Turret;

                StandardRocket standardRocket = rocket.gameObject.AddComponent<StandardRocket>();
                standardRocket.Initialize(null, targetPos, Config.RocketSpeed, false, false, Config.DamageMultiplier);
                return true;
            }

            internal void OnRocketFired()
            {
                nextShotTime = Time.time + Config.FireRate;

                if (lastAmmoItem != null)
                {
                    lastAmmoItem.UseItem();

                    if (lastAmmoItem.amount <= 0)
                    {
                        lastAmmoItem.RemoveFromContainer();
                        lastAmmoItem.Remove();
                        lastAmmoItem = null;
                    }
                }
                Turret.totalAmmoDirty = true;
            }

            protected string GetPrefabFromItem()
            {
                if (lastAmmoItem == null)
                    return ROCKET_BASIC_PREFAB;

                switch (lastAmmoItem.info.shortname)
                {                    
                    case ROCKET_FIRE_ITEM:
                        return ROCKET_FIRE_PREFAB;
                    case ROCKET_HV_ITEM:
                        return ROCKET_HV_PREFAB;
                    case ROCKET_BASIC_ITEM:                        
                    default:
                        return ROCKET_BASIC_PREFAB;
                }
            }
            #endregion

            #region Definitions
            private static ItemDefinition _basicRocketDefinition;
            private static ItemDefinition _incendiaryRocketDefinition;
            private static ItemDefinition _hvRocketDefinition;

            private static ItemDefinition BasicRocketDefinition
            {
                get
                {
                    if (!_basicRocketDefinition)
                        _basicRocketDefinition = ItemManager.FindItemDefinition(ROCKET_BASIC_ITEM);
                    return _basicRocketDefinition;
                }
            }

            private static ItemDefinition IncendiaryRocketDefinition
            {
                get
                {
                    if (!_incendiaryRocketDefinition)
                        _incendiaryRocketDefinition = ItemManager.FindItemDefinition(ROCKET_FIRE_ITEM);
                    return _incendiaryRocketDefinition;
                }
            }

            private static ItemDefinition HvRocketDefinition
            {
                get
                {
                    if (!_hvRocketDefinition)
                        _hvRocketDefinition = ItemManager.FindItemDefinition(ROCKET_HV_ITEM);
                    return _hvRocketDefinition;
                }
            }
            #endregion
        }

        private class AATurret : RocketTurret
        {
            public override ConfigData.TurretConfig Config => configData.AntiAir;
            
            private ConfigData.AntiAirTurret AAConfig => Config as ConfigData.AntiAirTurret;
            
            private readonly List<BaseEntity> _potentialTargets = new List<BaseEntity>();
            
            private float _nextScanTime;
            
            protected override void Awake()
            {
                base.Awake();
                type = TurretType.AA;
            }

            protected override void BuildCosmetics()
            {
                base.BuildCosmetics();

                AttachToTurret(ItemManager.CreateByName(COMPUTER_ITEM), new Vector3(0f, 0f, -0.52f), Vector3.zero, string.Empty);
                AttachToTurret(ItemManager.CreateByName(CAMERA_ITEM), new Vector3(0.2f, 0.1f, -0.1f), new Vector3(0, 90f, 0f));
                AttachToTurret(ItemManager.CreateByName(CAMERA_ITEM), new Vector3(-0.2f, 0.1f, -0.1f), new Vector3(0, 90f, 0f));
            }

            internal override void TargetScan()
            {
                if (Turret.IsOffline())                
                    return;

                if (Turret.HasTarget())
                {
                    Vector3 muzzlePos = Turret.eyePos.position + (Turret.eyePos.rotation * Vector3.forward * 0.5f);

                    if (Vector3.Distance(muzzlePos, Turret.target.transform.position) < Config.MinTargetingDistance || ShouldForgetTarget(Turret.target))
                        Turret.SetTarget(null);
                    return;
                }

                if (Time.time > _nextScanTime)
                {
                    _nextScanTime = Time.time + Random.Range(2.8f, 3.2f);
                    
                    _potentialTargets.Clear();
                    
                    _potentialTargets.AddRange(_patrolHelicopters);
                    if (Turret.targetTrigger.entityContents != null)
                        _potentialTargets.AddRange(Turret.targetTrigger.entityContents);
                    
                    AddTargetSet(_potentialTargets);
                }

                foreach (BaseEntity baseEntity in _potentialTargets)
                {
                    if (baseEntity is not BaseCombatEntity component || !component || component.IsDestroyed || !component.IsAlive()) 
                        continue;
                        
                    if (component.transform.position.y < tr.position.y)
                        continue;
                    
                    float distance = Vector3.Distance(component.transform.position, transform.position);
                    if (distance < Config.MinTargetingDistance || distance > Config.MaxTargetingDistance)
                        continue;
                    
                    if (!Turret.InFiringArc(component) || !Turret.ObjectVisible(component)) 
                        continue;
                        
                    if (!CanTargetEntity(component))
                        continue;

                    if (Turret.PeacekeeperMode())
                    {
                        if (!Turret.IsEntityHostile(component))
                        {
                            continue;
                        }
                        if (!Turret.target)
                        {
                            nextShotTime = Time.time + 1f;
                        }
                    }
                                
                    SetTarget(component);
                    break;
                }
            }
            
            private void AddTargetSet(List<BaseEntity> allTargets)
            {
                foreach (SamSite.ISamSiteTarget samSiteTarget in SamSite.ISamSiteTarget.serverList)
                {
                    Vector3 position = samSiteTarget.CenterPoint();
                    if (samSiteTarget is MLRSRocket or not BaseCombatEntity)
                        continue;

                    float distance = Vector3.Distance(position, transform.position);
                    if (distance < Config.MinTargetingDistance || distance > Config.MaxTargetingDistance)
                        continue;
                    
                    allTargets.Add(samSiteTarget as BaseCombatEntity);
                }
            }
           
            protected override bool CanTargetEntity(BaseCombatEntity component)
            {
          //      if (component.transform.position.y < tr.position.y || component is AutoTurret || component is RidableHorse2)
                    return false;

                if (component is PatrolHelicopter)
                    return AAConfig.Patrol;

                if (component is BaseHelicopter entity)
                {
                    if (entity is CH47Helicopter)
                        return AAConfig.CH47;

                    if (!AAConfig.Helicopters)
                        return false;

                    bool hasMounted = false;

                    for (int i = 0; i < entity.mountPoints?.Count; i++)
                    {
                        BaseMountable mountable = entity.mountPoints[i].mountable;
                        if (mountable.AnyMounted())
                        {
                            hasMounted = true;
                            BasePlayer basePlayer = mountable.GetMounted();
                            if (basePlayer)
                            {
                                if (Turret.IsAuthed(basePlayer))
                                    return false;

                                object obj = CallTargetingHook(Turret, basePlayer);
                                if (obj is bool b)
                                    return b;
                            }
                        }
                    }

                    return hasMounted;
                }

                if (component is HotAirBalloon hotAirBalloon && AAConfig.HAB)
                {
                    bool hasMounted = false;
                    for (int i = 0; i < hotAirBalloon.children?.Count; i++)
                    {
                        BasePlayer basePlayer = hotAirBalloon.children[i] as BasePlayer;
                        if (basePlayer)
                        {
                            hasMounted = true;
                            if (Turret.IsAuthed(basePlayer))                            
                                return false;

                            object obj = CallTargetingHook(Turret, basePlayer);
                            if (obj is bool b)
                                return b;
                        }
                    }

                    return hasMounted;
                }

                if (component is BasePlayer { isMounted: true } player)
                {
                    BaseVehicle baseVehicle = player.GetMounted().GetComponentInParent<BaseVehicle>();
                    if (!baseVehicle) 
                        return false;
                    
                    for (int i = 0; i < baseVehicle?.mountPoints?.Count; i++)
                    {
                        BaseMountable mountable = baseVehicle.mountPoints[i].mountable;
                        if (mountable.AnyMounted())
                        {
                            BasePlayer basePlayer = mountable.GetMounted();
                            if (basePlayer != null)
                            {
                                if (Turret.IsAuthed(basePlayer))
                                    return false;

                                object obj = CallTargetingHook(Turret, basePlayer);
                                if (obj is bool b)
                                    return b;
                            }
                        }
                    }

                    if (Turret.IsAuthed(player))
                        return false;

                    object obj1 = CallTargetingHook(Turret, player);
                    if (obj1 is bool obj2)
                        return obj2;

                    return true;

                }

                return false;
            }

            protected override bool ShouldForgetTarget(BaseCombatEntity baseCombatEntity)
            {
                return baseCombatEntity.transform.position.y < tr.position.y;
            }

            protected override bool FireRocket(Vector3 targetPos, BaseCombatEntity target = null)
            {
                if (Turret.IsOffline())
                    return false;

                Vector3 muzzlePos = Turret.eyePos.position + (Turret.eyePos.rotation * Vector3.forward * 0.5f);
                BaseEntity rocket = GameManager.server.CreateEntity(GetPrefabFromItem(), muzzlePos, Quaternion.Euler(targetPos - muzzlePos));
                rocket.enableSaving = false;
                rocket.Spawn();

                Effect.server.Run(ROCKET_FIRE_EFFECT, rocket.transform.position);

                if (AAConfig.RocketTrail)
                    AttachFireball(rocket);

                rocket.creatorEntity = (BasePlayer.FindAwakeOrSleeping(Turret.OwnerID.ToString()) as BaseEntity) ?? Turret;

                ConfigData.AntiAirTurret config = AAConfig;
                HeatSeekerRocket heatSeeker = rocket.gameObject.AddComponent<HeatSeekerRocket>();
                heatSeeker.Initialize(target, targetPos, config.RocketSpeed, true, true, config.DamageMultiplier);
                return true;
            }
        }

        private class JavelinTurret : RocketTurret
        {
            public override ConfigData.TurretConfig Config => configData.Javelin;
            
            protected override void Awake()
            {
                base.Awake();
                type = TurretType.Javelin;
            }

            protected override void BuildCosmetics()
            {
                base.BuildCosmetics();
                AttachToTurret(ItemManager.CreateByName(COMPUTER_ITEM), new Vector3(0f, 0, -0.52f), Vector3.zero, string.Empty);
            }

            protected override bool FireRocket(Vector3 targetPos, BaseCombatEntity target = null)
            {
                if (Turret.IsOffline())
                    return false;

                Vector3 muzzlePos = Turret.eyePos.position + (Turret.eyePos.rotation * Vector3.forward * 0.5f);
                
                BaseEntity rocket = GameManager.server.CreateEntity(GetPrefabFromItem(), muzzlePos, Quaternion.Euler(targetPos - muzzlePos));
                rocket.enableSaving = false;
                rocket.Spawn();

                Effect.server.Run(ROCKET_FIRE_EFFECT, rocket.transform.position);

                if (Config.RocketTrail)
                    AttachFireball(rocket);

                rocket.creatorEntity = (BasePlayer.FindAwakeOrSleeping(Turret.OwnerID.ToString()) as BaseEntity) ?? Turret;

                ConfigData.BasicTurret config = Config as ConfigData.BasicTurret;
                JavelinRocket javelinRocket = rocket.gameObject.AddComponent<JavelinRocket>();
                javelinRocket.Initialize(target, targetPos, config.RocketSpeed, config.SeekTarget, config.ProximityDetonation, config.DamageMultiplier);                
                return true;
            }
        }
        #endregion

        #region Rockets
        private class StandardRocket : MonoBehaviour
        {
            protected ServerProjectile projectile;
            protected TimedExplosive explosive;
            protected Transform tr;

            protected BaseCombatEntity target;

            internal List<DamageTypeEntry> damageTypes = Pool.Get<List<DamageTypeEntry>>();

            protected float speed;

            private const int DAMAGE_LAYERS = 1 << 8 | 1 << 11 | 1 << 13 | 1 << 15 | 1 << 17 | 1 << 21 | 1 << 27 | 1 << 30;//5846

            private void Awake()
            {
                projectile = GetComponent<ServerProjectile>();
                explosive = GetComponent<TimedExplosive>();

                damageTypes.AddRange(explosive.damageTypes);
                explosive.damageTypes.Clear();

                projectile.gravityModifier = 0f;

                explosive.CancelInvoke(explosive.Explode);
                explosive.Invoke(explosive.Explode, 30f);

                tr = projectile.transform;
            }

            protected virtual void OnDestroy()
            {
                DamageUtil.RadiusDamage(explosive.creatorEntity, explosive.LookupPrefab(), explosive.CenterPoint(), explosive.minExplosionRadius, explosive.explosionRadius, damageTypes, DAMAGE_LAYERS, true);
                if (explosive.creatorEntity != null && damageTypes != null)
                {
                    float damageAmount = 0f;
                    foreach (DamageTypeEntry damageType in damageTypes)
                    {
                        damageAmount += damageType.amount;
                    }
                }

                Pool.FreeUnmanaged(ref damageTypes);
            }

            internal virtual void Initialize(BaseCombatEntity target, Vector3 targetPosition, float speed, bool seekTarget, bool proximityDetonation, float damageMultiplier)
            {
                this.target = target;                
                this.speed = speed;

                ScaleDamage(damageMultiplier);

                tr.LookAt(targetPosition);
                projectile.InitializeVelocity(tr.forward * speed);
            }

            internal void ScaleDamage(float damageMultiplier)
            {
                foreach (DamageTypeEntry damageType in damageTypes)
                    damageType.amount *= damageMultiplier;
            }
        }

        private class HeatSeekerRocket : StandardRocket
        {
            protected Vector3 lastTargetPosition;
                        
            internal override void Initialize(BaseCombatEntity target, Vector3 targetPosition, float speed, bool seekTarget, bool proximityDetonation, float damageMultiplier)
            {               
                this.target = target;
                this.speed = speed;

                ScaleDamage(damageMultiplier);

                tr.LookAt(targetPosition);

                InvokeHandler.InvokeRepeating(this, AimThink, 0f, 0.15f);

                if (proximityDetonation)
                    InvokeHandler.InvokeRepeating(this, CheckProximity, 0.05f, 0.05f);
            }

            protected void AimThink()
            {
                if (target == null || target.IsDead())                                    
                    return;                

                if (target.transform.position == lastTargetPosition)
                    return;

                tr.LookAt(target.transform.position);
                projectile.InitializeVelocity(tr.forward * speed);

                lastTargetPosition = target.transform.position;
            }

            protected void CheckProximity()
            {
                if (target == null || target.IsDead() || Vector3.Distance(tr.position, target.transform.position) < 0.5f)
                {
                    explosive.Explode();
                }
            }
        }

        private class JavelinRocket : HeatSeekerRocket
        {
            private bool seekTarget;

            internal override void Initialize(BaseCombatEntity target, Vector3 targetPosition, float speed, bool seekTarget, bool proximityDetonation, float damageMultiplier)
            {                
                this.target = target;
                this.speed = speed;
                this.seekTarget = seekTarget;

                ScaleDamage(damageMultiplier);

                lastTargetPosition = target.transform.position;
                if (seekTarget)
                    InvokeHandler.Invoke(this, UpdateTargetPosition, 1f);

                explosive.minExplosionRadius = explosive.explosionRadius = configData.Javelin.ExplosionRadius;

                tr.LookAt(targetPosition);
                projectile.InitializeVelocity(tr.forward * speed);

                InvokeHandler.Invoke(this, MoveUpwards, 0.3f);

                if (proximityDetonation)
					//InvokeHandler (this, CheckProximity, 0.05f, 0.05f);
                    InvokeHandler.InvokeRepeating(this, CheckProximity, 0.05f, 0.05f);
            }

            private void UpdateTargetPosition()
            {
                if (target == null || target.IsDead())
                    return;

                lastTargetPosition = target.transform.position;

                InvokeHandler.Invoke(this, UpdateTargetPosition, 1f);
            }

            private void MoveUpwards()
            {
                tr.LookAt(tr.position + Vector3.up);
                projectile.InitializeVelocity(tr.forward * (speed * 1.25f));

                InvokeHandler.Invoke(this, MoveTowards, 1.25f);                
            }

            private void MoveTowards()
            {                
                tr.LookAt(new Vector3(lastTargetPosition.x, tr.position.y, lastTargetPosition.z));
                projectile.InitializeVelocity(tr.forward * speed);

                InvokeHandler.Invoke(this, DropToTarget, 0.15f);
            }

            private void DropToTarget()
            {
                if (Vector3Ex.Distance2D(tr.position, lastTargetPosition) < 10f)
                {
                    tr.LookAt(lastTargetPosition);
                    projectile.InitializeVelocity(tr.forward * speed);

                    InvokeHandler.InvokeRepeating(this, AimThink, 0.15f, 0.15f);
                    return;
                }

                tr.LookAt(new Vector3(lastTargetPosition.x, tr.position.y, lastTargetPosition.z));
                projectile.InitializeVelocity(tr.forward * speed);

                InvokeHandler.Invoke(this, DropToTarget, 0.15f);
            }
        }
        #endregion
        #endregion

        #region Commands      
        [ChatCommand("turret")]
        private void cmdTurret(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, JAVELIN_PERMISSION) && 
                !permission.UserHasPermission(player.UserIDString, BASIC_PERMISSION) && 
                !permission.UserHasPermission(player.UserIDString, AA_PERMISSION))
            {
                player.ChatMessage(Message("Error.NoPermission", player.userID));
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage(string.Format(Message("Chat.Title", player.userID), Version));

                player.ChatMessage(Message("Chat.Help.1", player.userID));

                if (permission.UserHasPermission(player.UserIDString, BASIC_PERMISSION))
                    player.ChatMessage(Message("Chat.Help.2", player.userID));

                if (permission.UserHasPermission(player.UserIDString, AA_PERMISSION))
                    player.ChatMessage(Message("Chat.Help.3", player.userID));

                if (permission.UserHasPermission(player.UserIDString, JAVELIN_PERMISSION))
                    player.ChatMessage(Message("Chat.Help.4", player.userID));

                player.ChatMessage(Message("Chat.Help.5", player.userID));
                return;
            }
            
            AutoTurret turret = FindEntityFromRay<AutoTurret>(player);

            switch (args[0].ToLower())
            {
                case "cost":
                    if (permission.UserHasPermission(player.UserIDString, BASIC_PERMISSION))
                        player.ChatMessage(string.Format(Message("Chat.Costs.Rocket", player.userID), configData.Basic.Cost.Select(x => string.Format(Message("Chat.Costs.Format", player.userID), x.Amount, ItemManager.itemDictionaryByName[x.Shortname].displayName.english)).ToSentence()));

                    if (permission.UserHasPermission(player.UserIDString, AA_PERMISSION))
                        player.ChatMessage(string.Format(Message("Chat.Costs.AA", player.userID), configData.AntiAir.Cost.Select(x => string.Format(Message("Chat.Costs.Format", player.userID), x.Amount, ItemManager.itemDictionaryByName[x.Shortname].displayName.english)).ToSentence()));

                    if (permission.UserHasPermission(player.UserIDString, JAVELIN_PERMISSION))
                        player.ChatMessage(string.Format(Message("Chat.Costs.Javelin", player.userID), configData.Javelin.Cost.Select(x => string.Format(Message("Chat.Costs.Format", player.userID), x.Amount, ItemManager.itemDictionaryByName[x.Shortname].displayName.english)).ToSentence()));
                    return;

                case "basic":                
                case "aa":                
                case "javelin":
                    if (!ParseType(args[0], out TurretType type))
                    {
                        player.ChatMessage(Message("Error.InvalidType", player.userID));
                        return;
                    }

                    if (!player.IsBuildingAuthed())
                    {
                        player.ChatMessage(Message("Error.NoPrivilege", player.userID));
                        return;
                    }

                    if (turret == null)
                    {
                        player.ChatMessage(Message("Error.NoTurret", player.userID));
                        return;
                    }

                    if (turret.GetComponent<RocketTurret>())
                    {
                        player.ChatMessage(Message("Error.IsTurret", player.userID));
                        return;
                    }

                    if (player.userID != turret.OwnerID)
                    {
                        if ((Friends == null || !Friends.Call<bool>("AreFriends", player.UserIDString, turret.OwnerID.ToString())) &&
                            (Clans == null || !Clans.Call<bool>("HasFriend", turret.OwnerID, player.userID)))
                        {
                            player.ChatMessage(Message("Error.NotOwner", player.userID));
                            return;
                        }
                    }

                    if (turret.AttachedWeapon != null)
                    {
                        player.ChatMessage(Message("Error.HasWeapon", player.userID));
                        return;
                    }

                    if (!permission.UserHasPermission(player.UserIDString, (type == TurretType.Basic ? BASIC_PERMISSION : type == TurretType.AA ? AA_PERMISSION : JAVELIN_PERMISSION)))
                    {
                        player.ChatMessage(Message("Error.NoPermission", player.userID));
                        return;
                    }

                    if (configData.Settings.LimitPerBuilding >= 0)
                    {
                        if (CountTurretsForBuilding(turret) >= configData.Settings.LimitPerBuilding)
                        {
                            player.ChatMessage(Message("Error.HitLimitBuilding", player.userID));
                            return;
                        }
                    }

                    if (!permission.UserHasPermission(player.UserIDString, NOLIMIT_PERMISSION))
                    {
                        int max = GetTurretLimit(player.UserIDString, type);
                        if (CountTurrets(player.userID, type) >= max)
                        {
                            player.ChatMessage(Message("Error.HitLimit", player.userID));
                            return;
                        }
                    }

                    if (!HasResources(player, type))
                    {
                        player.ChatMessage(Message("Error.NotEnoughResources", player.userID));
                        return;
                    }

                    TakeResources(player, type);

                    switch (type)
                    {
                        case TurretType.Basic:                            
                            turret.gameObject.AddComponent<RocketTurret>();
                            break;
                        case TurretType.AA:
                            turret.gameObject.AddComponent<AATurret>();
                            break;
                        case TurretType.Javelin:
                            turret.gameObject.AddComponent<JavelinTurret>();
                            break;                        
                    }

                    player.ChatMessage(string.Format(Message("Notification.Success.Create", player.userID), type));
                    return;

                case "remove":
                    if (turret == null)
                    {
                        player.ChatMessage(Message("Error.NoTurret", player.userID));
                        return;
                    }

                    if (!player.IsBuildingAuthed())
                    {
                        player.ChatMessage(Message("Error.NoPrivilege", player.userID));
                        return;
                    }

                    if (player.userID != turret.OwnerID)
                    {
                        if ((Friends == null || !Friends.Call<bool>("AreFriends", player.UserIDString, turret.OwnerID.ToString())) &&
                            (Clans == null || !Clans.Call<bool>("HasFriend", turret.OwnerID, player.userID)))
                        {
                            player.ChatMessage(Message("Error.NotOwner", player.userID));
                            return;
                        }
                    }

                    RocketTurret rocketTurret = turret.GetComponent<RocketTurret>();
                    if (rocketTurret == null)
                    {
                        player.ChatMessage(Message("Error.NotTurret", player.userID));
                        return;
                    }

                    ReturnResources(player, rocketTurret.type, configData.Settings.ReturnOnRemove);

                    UnityEngine.Object.Destroy(rocketTurret);
                    player.ChatMessage(Message("Notification.Success.Remove", player.userID));
                    return;

                default:
                    player.ChatMessage(Message("Error.Syntax", player.userID));
                    return;
            }
        }
        #endregion

        #region Config        
        private static ConfigData configData;
        private class ConfigData
        {
            public OtherSettings Settings { get; set; }

            [JsonProperty(PropertyName = "Rocket Turret")]
            public BasicTurret Basic { get; set; }

            [JsonProperty(PropertyName = "Javelin Turret")]
            public BasicTurret Javelin { get; set; }

            [JsonProperty(PropertyName = "AA Turret")]
            public AntiAirTurret AntiAir { get; set; }

            [JsonProperty(PropertyName = "VIP Turret Limits")]
            public Dictionary<string, Dictionary<TurretType, int>> VIPLimits { get; set; }

            public class OtherSettings
            {                
                [JsonProperty(PropertyName = "Return % of build costs when picked up")]
                public float ReturnOnPickup { get; set; }

                [JsonProperty(PropertyName = "Return % of build costs when rocket component is removed")]
                public float ReturnOnRemove { get; set; }

                [JsonProperty(PropertyName = "Turret limit per building (-1 is disabled)")]
                public int LimitPerBuilding{ get; set; }
            }

            public class BasicTurret : TurretConfig
            {
                [JsonProperty(PropertyName = "Use heat-seeking rockets")]
                public bool SeekTarget { get; set; }

                [JsonProperty(PropertyName = "Use proximity detonation")]
                public bool ProximityDetonation { get; set; }
            }

            public class AntiAirTurret : TurretConfig
            {               
                [JsonProperty(PropertyName = "Can target minicopters and scrap transport helicopters")]
                public bool Helicopters { get; set; }

                [JsonProperty(PropertyName = "Can target patrol helicopter")]
                public bool Patrol { get; set; }

                [JsonProperty(PropertyName = "Can target CH47")]
                public bool CH47 { get; set; }

                [JsonProperty(PropertyName = "Can target Hot Air Balloons")]
                public bool HAB { get; set; }
            }

            public class TurretConfig
            {
                [JsonProperty(PropertyName = "Rocket launcher skin ID")]
                public ulong LauncherSkin { get; set; }

                [JsonProperty(PropertyName = "Use emphasised rocket trail")]
                public bool RocketTrail { get; set; }

                [JsonProperty(PropertyName = "Damage multiplier")]
                public float DamageMultiplier { get; set; }

                [JsonProperty(PropertyName = "Explosion radius")]
                public float ExplosionRadius { get; set; }

                [JsonProperty(PropertyName = "Amount of time to lock on to a target (seconds)")]
                public float LockDuration { get; set; }

                [JsonProperty(PropertyName = "Minimum distance to acquire a target")]
                public float MinTargetingDistance { get; set; }

                [JsonProperty(PropertyName = "Maximum distance to acquire a target")]
                public float MaxTargetingDistance { get; set; }

                [JsonProperty(PropertyName = "Can target NPCs")]
                public bool TargetNPCs { get; set; }

                [JsonProperty(PropertyName = "Maximum turrets per player")]
                public int MaxPerPlayer { get; set; }
                                
                [JsonProperty(PropertyName = "Requires ammo to fire")]
                public bool RequiresAmmo { get; set; }

                [JsonProperty(PropertyName = "Fire rate (seconds)")]
                public float FireRate { get; set; }

                [JsonProperty(PropertyName = "Rocket speed (m/s)")]
                public float RocketSpeed { get; set; }

                [JsonProperty(PropertyName = "Cost to upgrade")]
                public List<ItemCost> Cost { get; set; }
                
                [JsonProperty(PropertyName = "Allowed rocket types (ammo.rocket.basic, ammo.rocket.hv, ammo.rocket.fire)")]
                public List<string> AllowedRockets { get; set; }

                public class ItemCost
                {                    
                    public string Shortname { get; set; }
                    public int Amount { get; set; }

                    public ItemCost() { }

                    public ItemCost(string shortname, int amount)
                    {
                        Shortname = shortname;
                        Amount = amount;
                    }
                }
            }
            public VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                AntiAir = new ConfigData.AntiAirTurret
                {
                    Cost = new List<ConfigData.TurretConfig.ItemCost>
                    {
                        new ConfigData.TurretConfig.ItemCost("cctv.camera", 2),
                        new ConfigData.TurretConfig.ItemCost("rocket.launcher", 1),
                        new ConfigData.TurretConfig.ItemCost("targeting.computer", 1),
                        new ConfigData.TurretConfig.ItemCost("metal.refined", 10),
                    },
                    DamageMultiplier = 2.5f,
                    ExplosionRadius = 4,
                    FireRate = 3.0f,
                    LauncherSkin = 0UL,
                    LockDuration = 1f,
                    MaxPerPlayer = 2,
                    MaxTargetingDistance = 80,
                    MinTargetingDistance = 4,
                    RequiresAmmo = true,
                    RocketSpeed = 30,
                    RocketTrail = false,
                    HAB = true,
                    Helicopters = true,
                    Patrol = true,
                    CH47 = true,
                    AllowedRockets = new List<string>
                    {
                        "ammo.rocket.basic",
                        "ammo.rocket.hv",
                        "ammo.rocket.fire"
                    }
                },
                Basic = new ConfigData.BasicTurret
                {
                    Cost = new List<ConfigData.TurretConfig.ItemCost>
                    {
                        new ConfigData.TurretConfig.ItemCost("rocket.launcher", 1),
                        new ConfigData.TurretConfig.ItemCost("metal.refined", 10),
                    },
                    DamageMultiplier = 1f,
                    ExplosionRadius = 4,
                    FireRate = 2.0f,
                    LauncherSkin = 0UL,
                    LockDuration = 1f,
                    MaxPerPlayer = 2,
                    MaxTargetingDistance = 40,
                    MinTargetingDistance = 4,
                    RequiresAmmo = true,
                    RocketSpeed = 20,
                    ProximityDetonation = true,
                    SeekTarget = false,
                    RocketTrail = false,
                    AllowedRockets = new List<string>
                    {
                        "ammo.rocket.basic",
                        "ammo.rocket.hv",
                        "ammo.rocket.fire"
                    }
                },                
                Javelin = new ConfigData.BasicTurret
                {
                    Cost = new List<ConfigData.TurretConfig.ItemCost>
                    {
                        new ConfigData.TurretConfig.ItemCost("rocket.launcher", 1),
                        new ConfigData.TurretConfig.ItemCost("targeting.computer", 1),
                        new ConfigData.TurretConfig.ItemCost("metal.refined", 10),
                    },
                    DamageMultiplier = 1f,
                    ExplosionRadius = 4,
                    FireRate = 4f,
                    LauncherSkin = 0UL,
                    LockDuration = 1f,
                    MaxPerPlayer = 2,
                    MaxTargetingDistance = 80,
                    MinTargetingDistance = 10,
                    RequiresAmmo = true,
                    RocketSpeed = 30,
                    ProximityDetonation = true,
                    SeekTarget = true,
                    RocketTrail = false,
                    AllowedRockets = new List<string>
                    {
                        "ammo.rocket.basic",
                        "ammo.rocket.hv",
                        "ammo.rocket.fire"
                    }
                },
                Settings = new ConfigData.OtherSettings
                {
                    ReturnOnRemove = 50,
                    ReturnOnPickup = 50,
                    LimitPerBuilding = -1
                },
                VIPLimits = new Dictionary<string, Dictionary<TurretType, int>>
                {
                    ["rocketturrets.vip1"] = new Dictionary<TurretType, int>
                    {
                        [TurretType.Basic] = 3,
                        [TurretType.Javelin] = 3,
                        [TurretType.AA] = 3,
                    },
                    ["rocketturrets.vip2"] = new Dictionary<TurretType, int>
                    {
                        [TurretType.Basic] = 5,
                        [TurretType.Javelin] = 3,
                        [TurretType.AA] = 4,
                    },
                    ["rocketturrets.vip3"] = new Dictionary<TurretType, int>
                    {
                        [TurretType.Basic] = 5,
                        [TurretType.Javelin] = 5,
                        [TurretType.AA] = 5,
                    },
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(2, 0, 0))
                configData = baseConfig;

            if (configData.Version < new VersionNumber(2, 0, 5))
            {
                configData.AntiAir.Helicopters = true;
                configData.AntiAir.HAB = true;
            }

            if (configData.Version < new VersionNumber(2, 0, 12))
                configData.VIPLimits = baseConfig.VIPLimits;

            if (configData.Version < new VersionNumber(2, 0, 19))
            {
                configData.Settings.LimitPerBuilding = -1;
                configData.AntiAir.DamageMultiplier = 2.5f;
                configData.Basic.DamageMultiplier = 1f;
                configData.Javelin.DamageMultiplier = 1f;
            }

            if (configData.Version < new VersionNumber(2, 0, 20))
            {
                configData.AntiAir.Patrol = true;
                configData.AntiAir.CH47 = true;
                configData.Basic.TargetNPCs = true;
                configData.Javelin.TargetNPCs = true;
                configData.AntiAir.TargetNPCs = true;
            }

            if (configData.Version < new VersionNumber(2, 0, 21))
            {
                configData.AntiAir.AllowedRockets = baseConfig.AntiAir.AllowedRockets;
                configData.Basic.AllowedRockets = baseConfig.Basic.AllowedRockets;
                configData.Javelin.AllowedRockets = baseConfig.Javelin.AllowedRockets;
            }

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Data Management
        private void SaveData()
        {
            if (ServerMgr.Instance.Restarting || !hasLoaded)
                return;

            storedData.launchers.Clear();

            for (int i = 0; i < RocketTurret.allTurrets.Count; i++)            
                storedData.Store(RocketTurret.allTurrets[i]);

            data.WriteObject(storedData);
        }

        private void LoadData()
        {
            try
            {
                storedData = data.ReadObject<StoredData>();
            }
            catch
            {
                storedData = new StoredData();
            }
        }

        private class StoredData
        {
            public Dictionary<ulong, Turret> launchers = new Dictionary<ulong, Turret>();

            public bool HasRestoreData(NetworkableId netid) => launchers.ContainsKey(netid.Value);
                        
            public void Store(RocketTurret rocketTurret) => launchers.Add(rocketTurret.Turret.net.ID.Value, new Turret(rocketTurret));
            
            public class Turret
            {
                public TurretType type;
                public string loadedAmmo;
                public int loadedCapacity;

                public Turret() { }

                public Turret(RocketTurret t)
                {
                    type = t.type;
                    if (t.BaseLauncher != null)
                    {
                        loadedAmmo = t.BaseLauncher.primaryMagazine.ammoType.shortname;
                        loadedCapacity = t.BaseLauncher.primaryMagazine.contents;
                    }
                }
            }
        }

        private enum TurretType { Basic, AA, Javelin }
        #endregion

        #region Localization        
        private string Message(string key, ulong playerId = 0UL) => lang.GetMessage(key, this, playerId == 0UL ? null : playerId.ToString());

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["Error.NoPermission"] = "<color=#939393>You do not have permission to use this command</color>",
            ["Chat.Title"] = "<color=#ce422b>RocketTurrets</color> v{0}",
            ["Chat.Help.1"] = "<color=#ce422b>/turret cost</color> - View costs associated with converting your turret",
            ["Chat.Help.2"] = "<color=#ce422b>/turret basic</color> - Convert a turret into a rocket turret",
            ["Chat.Help.3"] = "<color=#ce422b>/turret aa</color> - Convert a turret into a anti-aircraft turret",
            ["Chat.Help.4"] = "<color=#ce422b>/turret javelin</color> - Convert a turret into a javelin turret",
            ["Chat.Help.5"] = "<color=#ce422b>/turret remove</color> - Restore a turret back to the default AutoTurret",
            ["Chat.Costs.Rocket"] = "<color=#ce422b>Rocket Turret</color> : {0}",
            ["Chat.Costs.AA"] = "<color=#ce422b>AA Turret</color> : {0}",
            ["Chat.Costs.Javelin"] = "<color=#ce422b>Javelin Turret</color> : {0}",
            ["Chat.Costs.Format"] = "<color=#939393>{0}x {1}</color>",
            ["Error.InvalidType"] = "Invalid type entered. Type <color=#ce422b>/turret</color> for help",
            ["Error.NoPrivilege"] = "You must have <color=#ce422b>building privilege</color> to use this command",
            ["Error.NoTurret"] = "<color=#939393>No turret found</color>",
            ["Error.IsTurret"] = "<color=#939393>This turret is already a rocket turret variant</color>",
            ["Error.HasWeapon"] = "<color=#939393>This turret already has a weapon attached. Remove the weapon to create a rocket turret</color>",
            ["Error.NotOwner"] = "<color=#939393>You must be friends or clan mates with the owner of the turret</color>",
            ["Error.HitLimit"] = "<color=#939393>You already have the maximum amount of that turret type</color>",
            ["Error.HitLimitBuilding"] = "<color=#939393>This building already has the maximum amount of turrets allowed</color>",
            ["Error.NotEnoughResources"] = "<color=#939393>You do not have the required resources for that turret type</color>",
            ["Notification.Success.Create"] = "You have successfully created a <color=#ce422b>{0}</color> turret!",
            ["Error.NotTurret"] = "<color=#939393>This turret is not a rocket turret variant</color>",
            ["Notification.Success.Remove"] = "<color=#939393>You have successfully removed the rocket turret variant!</color>",
            ["Error.Syntax"] = "<color=#939393>Invalid Syntax!</color>",
        };
        #endregion
    }
}
