using System.Collections.Generic;
using System.Collections;
using Oxide.Core.Configuration;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using System;
using System.Globalization;
using VLB;
using Rust;
using Facepunch;
using Oxide.Game.Rust.Cui;
using static BaseVehicle;
using ProtoBuf;
using ConVar;
using HarmonyLib;


namespace Oxide.Plugins
{
    [Info("BetterSentryTurrets", "rustmods.ru", "1.7.3")]
    [Description("Player Placed Sentry Turret")]
    public class BetterSentryTurrets : RustPlugin
    {
        #region vars
        [PluginReference]
        private Plugin TurretLoadouts, TrainHomes;

        TurretEntity pcdData;
        private DynamicConfigFile PCDDATA;

        public AutoTurret.UpdateAutoTurretScanQueue updateAutoTurretScanQueueOriginal;

        private const string sentryPrefabString = "assets/content/props/sentry_scientists/sentry.scientist.static.prefab";
        private const string switchPrefabString = "assets/prefabs/deployable/playerioents/simpleswitch/switch.prefab";
        private const string lootname = "autoturret";
        private const string theAdmin = "BetterSentryTurrets.admin";
        private const ulong itemSkin = 3184234034;
        private Dictionary<string, string> limagelist;
        private static BetterSentryTurrets _;
        private static float requiredPower = 9;
        private static float scanRadius = 50;
        private static bool isSamSiteAlso;
        private static List<PatrolHelicopter> _allPatrolHelicopters = new List<PatrolHelicopter>();
        public Dictionary<ulong, NPCAutoTurret> lootingPlayer = new Dictionary<ulong, NPCAutoTurret>();
        public static ProtectionProperties newProtection = null;
        public static ProtectionProperties newProtectionFull = null;

        private static float[] protectionSettings = new float[] { 1, 1, 1, 1, 1, 0.8f, 1, 1, 1, 0.9f, 0.5f, 0.5f, 1, 1, 0, 0.5f, 0, 1, 1, 0, 1, 0.9f, 0, 1, 0 };
        private static float[] protectionSettingsFull = new float[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };

        private List<TurretMod> _allMods = new List<TurretMod>();
        private AutoTurret RefrenceTurret = null;
        private GameObjectRef reloadEffectOrig = null;
        private static bool active = false;

        #endregion

        #region Init/Unload
        private void Init()
        {
            _ = this;
            PCDDATA = Interface.Oxide.DataFileSystem.GetFile($"{Name}/Turret_Data");
            LoadData();
        }

        private void OnServerInitialized()
        {
            inputtextOpen.Clear();
            newProtection = ScriptableObject.CreateInstance<ProtectionProperties>();
            newProtectionFull = ScriptableObject.CreateInstance<ProtectionProperties>();
            updateAutoTurretScanQueueOriginal = AutoTurret.updateAutoTurretScanQueue;
            AutoTurret.updateAutoTurretScanQueue = new CustomScanQueue();
            int total = 0;

            permission.RegisterPermission(theAdmin, this);
            requiredPower = configData.settings.powerRequirement - 1;
            isSamSiteAlso = configData.settings.isSamSite;
            if (configData.settings.scanDistance > 0)
                scanRadius = configData.settings.scanDistance;

            if (configData.settings.aimCone <= 0)
            {
                configData.settings.aimCone = 4f;
                SaveConfig();
            }

            foreach (var found in BaseNetworkable.serverEntities)
            {
                if (found is AutoTurret)
                {
                    AutoTurret newTurret = found as AutoTurret;
                    if (!AutoTurret.updateAutoTurretScanQueue.Contains(newTurret))
                        newTurret.ScheduleForTargetScan();
                }

                if (found is PatrolHelicopter)
                {
                    if (!_allPatrolHelicopters.Contains(found as PatrolHelicopter))
                        _allPatrolHelicopters.Add(found as PatrolHelicopter);
                }
                else if (found != null && !found.IsDestroyed && found is NPCAutoTurret && (found as NPCAutoTurret).OwnerID != 0UL)
                {
                    total++;
                    if (RefrenceTurret == null)
                    {
                        RefrenceTurret = GameManager.server.CreateEntity("assets/prefabs/npc/autoturret/autoturret_deployed.prefab", Vector3.zero)?.GetComponent<AutoTurret>();
                        RefrenceTurret.Spawn();

                        if (RefrenceTurret.GetComponent<GroundWatch>() != null)
                            UnityEngine.Object.DestroyImmediate(RefrenceTurret.GetComponent<GroundWatch>());
                        if (RefrenceTurret.GetComponent<DestroyOnGroundMissing>() != null)
                            UnityEngine.Object.DestroyImmediate(RefrenceTurret.GetComponent<DestroyOnGroundMissing>());

                        reloadEffectOrig = RefrenceTurret.reloadEffect;
                    }

                    (found as NPCAutoTurret).reloadEffect = reloadEffectOrig;
                    _allMods.Add((found as NPCAutoTurret).GetOrAddComponent<TurretMod>());
                }
            }
            if (total > 0)
                PrintWarning($"Found and reactivated {total} sentry turrets.");

            if (RefrenceTurret != null)
                RefrenceTurret.Kill();

            if (pcdData != null)
            {
                pcdData.Health.Clear();
                SaveData();
            }
        }

        private void Unload()
        {
            if (updateAutoTurretScanQueueOriginal != null)
            {
                AutoTurret.updateAutoTurretScanQueue = updateAutoTurretScanQueueOriginal;
                foreach (var found in BaseNetworkable.serverEntities)
                {
                    if (found is AutoTurret)
                    {
                        AutoTurret newTurret = found as AutoTurret;
                        if (!AutoTurret.updateAutoTurretScanQueue.Contains(newTurret))
                            newTurret.ScheduleForTargetScan();
                    }
                }
            }

            if (RefrenceTurret != null)
                RefrenceTurret.Kill();

            if (_allMods != null)
            foreach (var mods in _allMods)
            {
                UnityEngine.Object.DestroyImmediate(mods);
            }

            if (BasePlayer.activePlayerList != null)
            foreach (BasePlayer current in BasePlayer.activePlayerList)
            {
                DestroyCuiAll(current);
                current?.SendConsoleCommand("gametip.hidegametip");
            }

            SaveData();
            _ = null;
        }

        void LoadData()
        {
            try
            {
                pcdData = Interface.GetMod().DataFileSystem.ReadObject<TurretEntity>($"{Name}/Turret_Data") ?? new TurretEntity();
            }
            catch
            {
                PrintWarning("Couldn't load Farm_Data, creating new TurretEntity file");
                pcdData = new TurretEntity();
            }
        }
        class TurretEntity
        {
            public Dictionary<ulong, float> Health = new Dictionary<ulong, float>();
        }

        void SaveData()
        {
            PCDDATA.WriteObject(pcdData);
        }

        public BaseNetworkable FindEntity(ulong netID)
        {
            return BaseNetworkable.serverEntities.Find(new NetworkableId(netID));
        }
        #endregion

        #region Config
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Turret Settings")]
            public Settings settings { get; set; }

            public class Settings
            {
                [JsonProperty(PropertyName = "Require gun")]
                public bool gunRequired { get; set; }
                [JsonProperty(PropertyName = "Require Ammunition")]
                public bool amoRequired { get; set; }
                [JsonProperty(PropertyName = "Make Turret Work With No Power To Switch")]
                public bool powerRequirementNeeded { get; set; }
                [JsonProperty(PropertyName = "Required Amount Of Power To Switch")]
                public float powerRequirement { get; set; }
                [JsonProperty(PropertyName = "Turret Gun Scan Distance")]
                public float scanDistance { get; set; }
                [JsonProperty(PropertyName = "Turret Start Health")]
                public float modhealth { get; set; }
                [JsonProperty(PropertyName = "Allowed To Also Be A SamSite")]
                public bool isSamSite { get; set; }
                [JsonProperty(PropertyName = "Sam Allowed To Target PatrolHelicopter")]
                public bool isAl1owedPatrol { get; set; }
                [JsonProperty(PropertyName = "Max Turret Placement In Range")]
                public int maxPlace { get; set; }
                [JsonProperty(PropertyName = "Max Turret Scan Range")]
                public float scanRangePlace { get; set; }
                [JsonProperty(PropertyName = "Turret AimCone")]
                public float aimCone { get; set; }
            }

            public Oxide.Core.VersionNumber Version { get; set; }
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
                settings = new ConfigData.Settings
                {
                    powerRequirement = 10.0f,
                    powerRequirementNeeded = false,
                    gunRequired = false,
                    amoRequired = true,
                    scanDistance = 50f,
                    modhealth = 1500f,
                    isSamSite = false,
                    isAl1owedPatrol = false,
                    maxPlace = 12,
                    scanRangePlace = 40,
                    aimCone = 4
                },

                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(1, 1, 0))
                configData = baseConfig;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion Config

        #region Item
        private enum Type
        {
            None,
            SentryTurretItem,
            gun
        }

        private Dictionary<Type, CustomItem> Items = new Dictionary<Type, CustomItem>
        {
            [Type.SentryTurretItem] = new CustomItem
            {
                DisplayName = "Sentry Turret",
                ShortName = "autoturret",
                skinID = 3184234034
            },
            [Type.gun] = new CustomItem
            {
                DisplayName = "Sentry Gun",
                ShortName = "lmg.m249",
                skinID = 3197480971
            }
        };

        private class CustomItem
        {
            public string DisplayName;
            public string ShortName;
            public ulong skinID;

            public Item CreateItem(int amount)
            {
                Item item = ItemManager.CreateByPartialName(ShortName, amount, skinID);
                item.name = DisplayName;
                item.MarkDirty();

                return item;
            }
        }
        #endregion

        #region Commands
        [ConsoleCommand("sentry")]
        private void CmdConsoleSentry(ConsoleSystem.Arg args)
        {
            if (args == null || args.Args.Length < 2) return;

            var playerRun = args.Player();
            if (playerRun != null && !permission.UserHasPermission(playerRun.userID.ToString(), theAdmin))
            {
                SendReply(playerRun, lang.GetMessage("nope", this, playerRun.UserIDString));
                return;
            }

            var ids = default(ulong);
            if (!ulong.TryParse(args.Args[0], out ids))
            {
                return;
            }

            var total = default(int);
            if (!int.TryParse(args.Args[1], out total))
            {
                return;
            }

            BasePlayer player = BasePlayer.FindByID(ids);

            if (player != null && total > 0)
            {
                Item dropItem = Items[Type.SentryTurretItem].CreateItem(total);
                if (dropItem.MoveToContainer(player.inventory.containerBelt, -1, true))
                {
                    SendReply(player, lang.GetMessage("gave", this));
                    return;
                }
                else if (dropItem.MoveToContainer(player.inventory.containerMain, -1, true))
                {
                    SendReply(player, lang.GetMessage("gave", this));
                    return;
                }
                Vector3 velocity = Vector3.zero;
                dropItem.Drop(player.transform.position + new Vector3(0.5f, 1f, 0), velocity);
                SendReply(player, lang.GetMessage("droped", this));
            }
        }

        [ChatCommand("sentry")]
        private void CmdChatGetSentry(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.userID.ToString(), theAdmin))
            {
                SendReply(player, lang.GetMessage("nope", this, player.UserIDString));
                return;
            }

            if (args.Length < 1 || args == null)
            {
                SendReply(player, string.Format(lang.GetMessage("usagecommand", this, player.UserIDString)));
                return;
            }

            var total = default(int);
            if (!int.TryParse(args[0], out total))
            {
                SendReply(player, string.Format(lang.GetMessage("usagecommand", this, player.UserIDString)));
                return;
            }

            if (total > 0)
            {
                GiveItemSentry(player, total);
            }
            else
                SendReply(player, string.Format(lang.GetMessage("usagecommand", this, player.UserIDString)));
        }

        private void GiveItemSentry(BasePlayer player, int total = 1)
        {
            Item dropItem = Items[Type.SentryTurretItem].CreateItem(total);
            player.GiveItem(dropItem, BaseEntity.GiveItemReason.Generic);
        }
        #endregion

        #region Hooks

        private void OnLootEntity(BasePlayer player, NPCAutoTurret entity)
        {
            if (!lootingPlayer.ContainsKey(player.userID))
                lootingPlayer.Add(player.userID, entity);
            else lootingPlayer[player.userID] = entity;

            BuildCuiScreen(player, entity);
        }

        void OnLootEntityEnd(BasePlayer player, NPCAutoTurret entity)
        {
            if (lootingPlayer.ContainsKey(player.userID))
                lootingPlayer.Remove(player.userID);
            DestroyCui(player);
        }

        private void OnEntityTakeDamage(NPCAutoTurret entity, HitInfo hitInfo)
        {
            if (entity == null || hitInfo == null) return;

            var type = hitInfo?.damageTypes?.GetMajorityDamageType() ?? Rust.DamageType.Generic;
            if (type == Rust.DamageType.Suicide) return;

            var attacker = hitInfo?.Initiator as AutoTurret;
            if (attacker != null)
            {
                hitInfo?.damageTypes?.ScaleAll(0.1f);
            }
        }

        private void OnEntitySpawned(PatrolHelicopter entity)
        {
            if (!_allPatrolHelicopters.Contains(entity))
                _allPatrolHelicopters.Add(entity);
        }

        private object OnInterferenceUpdate(AutoTurret turret)
        {
            if (turret == null || (turret.skinID == itemSkin && turret is NPCAutoTurret))
                return true;

            if (turret.skinID == 14922524)
                return null;

            if (!turret.IsOn())
                return null;

            float num = 0.0f;
            foreach (AutoTurret nearbyTurret in turret.nearbyTurrets)
            {
                if (!nearbyTurret.isClient && nearbyTurret.IsValid() && nearbyTurret.gameObject.activeSelf && !nearbyTurret.EqualNetID(turret.net.ID) && nearbyTurret.IsOn() && nearbyTurret.skinID != itemSkin && !nearbyTurret.HasInterference() && nearbyTurret.skinID != 14922524)
                    ++num;
            }
            turret.SetFlag(BaseEntity.Flags.OnFire, (double)num >= (double)Sentry.maxinterference);

            return true;
        }

        private object OnTurretLoadoutFill(BasePlayer player, AutoTurret turret)
        {
            if (turret.skinID == itemSkin && turret is not NPCAutoTurret)
                return false;

            return null;
        }

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            AutoTurret turretOriginal = go?.ToBaseEntity()?.GetComponent<AutoTurret>();
            if (turretOriginal == null || turretOriginal.skinID != itemSkin || turretOriginal is NPCAutoTurret)
                return;

            BasePlayer player = plan.GetOwnerPlayer();

            if (player == null)
                return;

            ulong ownerID = player.userID;

            Vector3 position = turretOriginal.transform.position;
            Quaternion rotation = turretOriginal.transform.rotation;
            Translate.Phrase title = turretOriginal.panelTitle;

            GameObjectRef reloadEffectOrig = turretOriginal.reloadEffect;

            if (TurretMod.GetNearbyTurrets(turretOriginal, configData.settings.scanRangePlace) > configData.settings.maxPlace)
            {
                GiveItemSentry(player);
                GameTipMessage(player, string.Format(lang.GetMessage("maxplace", this, player.UserIDString), configData.settings.maxPlace));

                NextTick(() =>
                {
                    if (turretOriginal != null && !turretOriginal.IsDestroyed)
                    {
                        turretOriginal.inventory.Clear();
                        turretOriginal.Kill();
                        turretOriginal.SendNetworkUpdate();
                        ItemManager.DoRemoves();
                    }
                });
                return;
            }
            else
            {
                NPCAutoTurret newTurret = GameManager.server.CreateEntity(sentryPrefabString, position, rotation)?.GetComponent<NPCAutoTurret>();

                if (newTurret == null)
                {
                    GiveItemSentry(player);
                }
                else
                {
                    newTurret.reloadEffect = reloadEffectOrig;
                    newTurret.OwnerID = ownerID;
                    newTurret.skinID = itemSkin;

                    newTurret.Spawn();

                    TurretMod newMod = newTurret.GetOrAddComponent<TurretMod>();
                    newMod.OwnerPlayer = player;

                    _allMods.Add(newMod);

                    newTurret.socketTransform = newTurret.muzzlePos;
                    newTurret.panelTitle = title;
                    newTurret.SetIsOnline(false);
                    newTurret.SetPeacekeepermode(false);
                    float health = configData.settings.modhealth;
                    newTurret._maxHealth = health;
                    newTurret.health = health;

                    Interface.Oxide.CallHook("OnEntityBuilt", plan, newTurret.gameObject);

                    if (newTurret.authorizedPlayers.Count <= 0)
                    {
                        newTurret.authorizedPlayers.Add(new PlayerNameID
                        {
                            userid = player.userID,
                            username = player.displayName
                        });
                    }
                }

                NextTick(() =>
                {
                    if (turretOriginal != null && !turretOriginal.IsDestroyed)
                    {

                        BaseEntity parent = turretOriginal.GetParentEntity();
                        if (parent != null)
                        {
                            if (newTurret != null && parent is TrainCar || (bool)TrainHomes?.Call("IsEntityFromBaseWagon", parent.net.ID.Value))
                                newTurret.SetParent(parent, true, true);
                        }

                        turretOriginal.inventory.Clear();
                        turretOriginal.Kill();
                        turretOriginal.SendNetworkUpdate();
                        ItemManager.DoRemoves();
                    }
                });
            }
        }

        private object CanPickupEntity(BasePlayer player, NPCAutoTurret turret)
        {
            if (turret != null && turret.OwnerID != 0UL && turret.skinID == itemSkin)
            {
                TurretMod mod = turret?.GetComponent<TurretMod>();
                if (mod != null)
                    mod.isInPickUp = true;
                GiveItemSentry(player);
                turret?.Kill();
                return false;
            }
            return null;
        }

        private object OnSwitchToggle(ElectricSwitch entity, BasePlayer player)
        {
            TurretMod mod = entity?.GetComponentInParent<TurretMod>();
            if (mod != null)
            {
                if (!mod.isAuthedPlayer(player))
                    return false;

                mod.onSwitchToggle();
            }
            return null;
        }

        private object OnTurretTarget(NPCAutoTurret turret, BasePlayer player)
        {
            if (player == null || turret.OwnerID == 0UL)
                return null;

            if (turret.GetAttachedWeapon() == null)
                return true;

            return null;
        }

        ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            if (item != null && item.skin == 3197480971 && container.GetEntityOwner() != null && container.GetEntityOwner() is BasePlayer)
            {
                return ItemContainer.CanAcceptResult.CannotAccept;
            }
            return null;
        }

        object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (item != null && item.skin == 3197480971)
                return false;

            return null;
        }

        public void turretLoadoutsFill(BasePlayer OwnerPlayer, AutoTurret turret)
        {
            if (OwnerPlayer != null)
            {
                turret.inventory.allowedContents = ItemContainer.ContentsType.Generic;
                turret.inventory.canAcceptItem = null;
                turret.inventory.capacity = 7;

                TurretLoadouts?.Call("API_FillTurret", OwnerPlayer, turret);

                if (!configData.settings.gunRequired)
                {
                    Item slot = turret.inventory.GetSlot(0);
                    if (slot != null)
                    {
                        slot.UseItem(slot.amount);
                    }
                }
            }
        }

        #endregion Hooks

        #region TurretMod

        public class CustomScanQueue : AutoTurret.UpdateAutoTurretScanQueue
        {
            public override void RunJob(AutoTurret entity)
            {
                if (!this.ShouldAdd(entity))
                    return;

                TurretMod mono = entity.GetComponent<TurretMod>();
                if (mono != null)
                    mono.TargetScan();
                else
                    entity.TargetScan();
            }
        }

        private void OnEntityKill(NPCAutoTurret entity)
        {
            entity?.GetComponent<TurretMod>()?.dropLoot();
        }

        public const string cameraPrefab = "assets/prefabs/deployable/drone/drone.deployed.prefab";
        public static Dictionary<Drone, TurretModInfo> _allCCTV = new Dictionary<Drone, TurretModInfo>();
        public class TurretModInfo
        {
            public TurretMod mod;
            public BasePlayer player;
        }

        private void OnBookmarkControlStarted(ComputerStation computerStation, BasePlayer player, string bookmarkName, Drone cctvRc)
        {
            if (cctvRc == null)
                return;

            computerStation.CancelInvoke(new Action(computerStation.ControlCheck));

            TurretMod controler = cctvRc?.GetComponentInParent<TurretMod>();
            if (controler != null)
            {
                  controler.StartControl(player);// IsBeingControlled = true;
            }
        }

        private void OnBookmarkControlEnded(ComputerStation station, BasePlayer player, Drone cctvRc)
        {
            if (cctvRc == null)
                return;

            TurretMod controler = cctvRc?.GetComponentInParent<TurretMod>();
            if (controler != null)
            {
                controler.StopControl(); // IsBeingControlled = false;
            }
        }

        public class TurretMod : MonoBehaviour
        {
            public AutoTurret turret { get; private set; }
            private Vector3 position { get; set; }
            public SphereCollider col { get; set; }
            private ElectricSwitch button { get; set; }
            private Item gunItem { get; set; }
            private SamMod samMod { get; set; }
            public BasePlayer OwnerPlayer { get; set; }
            public bool isInPickUp { get; set; }
            public static Dictionary<string, float> OldCone = new Dictionary<string, float>();
            private UIMonoSentry uiMono { get; set; }
            private BaseProjectile heldItem { get; set; }
            private bool destroyran { get; set; }
            public Drone camera { get; private set; }
            public string rcIdentifier { get; set; }
            public bool IsBeingControlled { get; set; }

            private void Awake()
            {
                turret = GetComponent<AutoTurret>();
                position = turret.transform.position;
                Invoke("runHookCalls", 0.01f);
                Invoke("CreateInventory", 0.03f);
                turret.aimCone = _.configData.settings.aimCone;
                AddOrGetButton();
                turret.dropsLoot = false;
                turret.baseProtection = newProtection;
                turret.baseProtection.amounts = protectionSettings;

                if (!_.configData.settings.powerRequirementNeeded)
                    InvokeRepeating(nameof(HavePowerStill), 5f, 5f);
                turret.GetOrAddComponent<DestroyOnGroundMissing>();
                turret.GetOrAddComponent<GroundWatch>();

                turret.sightRange = scanRadius + 5;

                col = turret.targetTrigger.GetComponent<SphereCollider>();
                col.radius = scanRadius;

                if (turret.IsInvoking(new Action(turret.ServerTick)))
                    turret.CancelInvoke(new Action(turret.ServerTick));

                turret.InvokeRepeating(new Action(ServerTick), UnityEngine.Random.Range(0.0f, 1f), 0.015f);

                if (isSamSiteAlso)
                {
                    samMod = turret.gameObject.GetComponent<SamMod>();
                    if (samMod != null)
                        UnityEngine.Object.Destroy(samMod);

                    samMod = turret.gameObject.AddComponent<SamMod>();
                    samMod.EnsureReloaded();
                }
                if (_.configData.settings.powerRequirementNeeded)
                {
                    button?.UpdateFromInput((int)_.configData.settings.powerRequirement + 1, 0);
                    if (button != null && button.IsOn())
                        turret.SetIsOnline(true);
                }

                Item slot = turret.inventory.GetSlot(0);

                if (slot != null)
                {
                    BaseProjectile component = slot.GetHeldEntity().GetComponent<BaseProjectile>();
                    if (component != null)
                    {
                        if (!OldCone.ContainsKey(component.name))
                            OldCone.Add(component.name, component.aimCone);

                        component.aimCone = turret.aimCone;
                        if (!component.name.Contains("minigun"))
                            component.SetFlag(BaseEntity.Flags.Disabled, true);
                    }
                }

                uiMono = UIMonoSentry.AddToEntity(turret);

                float health = _.configData.settings.modhealth;
                turret._maxHealth = health;

                if (_ != null && turret != null && !turret.IsDestroyed)
                {
                    if (_.pcdData.Health.ContainsKey(turret.net.ID.Value))
                    {
                        turret.health = _.pcdData.Health[turret.net.ID.Value];
                    }
                }

                turret.SendNetworkUpdate();

                turret.Invoke(new Action(SpawnCamera), 1f);
            }

            public void StartControl(BasePlayer controlerPlayer) { if (camera != null) _allCCTV[camera] = new TurretModInfo() { mod = this, player = controlerPlayer };  this.IsBeingControlled = true; }
            public void StopControl() { if (camera != null && _allCCTV.ContainsKey(camera)) _allCCTV.Remove(camera); this.IsBeingControlled = false; }

            private void SpawnCamera()
            {
                foreach (var child in turret.children)
                {
                    if (child is Drone)
                    {
                        camera = child.GetComponent<Drone>();
                        camera.CancelInvoke();
                        camera.baseProtection = newProtectionFull;
                        camera.baseProtection.amounts = protectionSettingsFull;
                        camera.SendNetworkUpdate();
                        camera.SetFlag(BaseEntity.Flags.Disabled, true);
                        camera.enabled = false;
                        if (camera.body != null)
                            UnityEngine.Object.DestroyImmediate(camera.body);
                        rcIdentifier = camera.rcIdentifier;
                        return;
                    }
                }

                camera = GameManager.server.CreateEntity(cameraPrefab, new Vector3(-0.38f, 0.28f, -1.3f), Quaternion.Euler(0f, 0, 0)) as Drone;
                camera.Spawn();
                if (camera.body != null)
                    UnityEngine.Object.DestroyImmediate(camera.body);
                camera.SetParent(turret, StringPool.Get(turret.socketTransform.name));
                camera.CancelInvoke();
                camera.SetFlag(BaseEntity.Flags.Disabled, true);
                camera.enabled = false;
                camera.baseProtection = newProtectionFull;
                camera.baseProtection.amounts = protectionSettingsFull;
                rcIdentifier = camera.rcIdentifier;
                camera.SendNetworkUpdate();
            }

            public void UpdateIdentifier(string newID)
            {
                if (camera == null)
                    return;

                string rcIdentifier = camera.rcIdentifier;
                if (!RemoteControlEntity.IDInUse(newID))
                {
                    camera.rcIdentifier = newID;
                    this.rcIdentifier = newID;
                }
                camera.SendNetworkUpdate();
            }

            private void AddGunAndLockSlot()
            {
                Item slot = turret.inventory.GetSlot(0);

                if (slot == null)
                {
                    gunItem = _.Items[Type.gun].CreateItem(1);

                    BaseProjectile component = gunItem.GetHeldEntity().GetComponent<BaseProjectile>();
                    if (component != null)
                    {
                        if (!_.configData.settings.amoRequired)
                            component.primaryMagazine.contents = 100;
                        else
                            component.primaryMagazine.contents = 0;

                        component.aimCone = _.configData.settings.aimCone;

                    }
                    gunItem.MoveToContainer(turret.inventory, 0);

                }
                else
                {
                    gunItem = slot;
                }
            }

            public void ServerTick()
            {
                if (turret.isClient || turret.IsDestroyed)
                    return;
                float sinceLastServerTick = (float)(double)turret.timeSinceLastServerTick;
                turret.timeSinceLastServerTick = (RealTimeSinceEx)0.0;
                if (!turret.IsOnline())
                    turret.OfflineTick();
                else if (!this.IsBeingControlled)
                {
                    if (turret.HasTarget())
                        TargetTick();
                    else
                        turret.IdleTick(sinceLastServerTick);
                }
                if (!this.IsBeingControlled)
                {
                    turret.UpdateFacingToTarget(sinceLastServerTick);
                    if (!turret.totalAmmoDirty || (double)UnityEngine.Time.time <= (double)turret.nextAmmoCheckTime)
                        return;
                    turret.UpdateTotalAmmo();
                    turret.totalAmmoDirty = false;
                    turret.nextAmmoCheckTime = UnityEngine.Time.time + 0.5f;
                }
                else
                    turret.UpdateAiming(sinceLastServerTick);
            }

            public void TargetScan()
            {
                if (!turret.authDirty && !turret.hasPotentialUnauthedTarget)
                    return;
                if (turret.HasInterference())
                {
                    if (!turret.HasTarget())
                        return;
                    turret.SetTarget((BaseCombatEntity)null);
                }
                else
                {
                    turret.hasPotentialUnauthedTarget = false;
                    turret.authDirty = false;
                    if (turret.HasTarget() || turret.IsOffline() || turret.IsBeingControlled)
                        return;
                    if (turret.targetTrigger.entityContents != null)
                    {
                        foreach (BaseEntity entityContent in turret.targetTrigger.entityContents)
                        {
                            BaseCombatEntity baseCombatEntity = entityContent as BaseCombatEntity;
                            if (!((UnityEngine.Object)baseCombatEntity == (UnityEngine.Object)null))
                            {

                                BasePlayer player = baseCombatEntity as BasePlayer;
                                if ((UnityEngine.Object)player != (UnityEngine.Object)null && (turret.IsAuthed(player) || Ignore(player)))
                                    continue;

                                if (!turret.hasPotentialUnauthedTarget)
                                    turret.hasPotentialUnauthedTarget = true;
                                if ((!turret.PeacekeeperMode()) && baseCombatEntity.IsAlive() && turret.ShouldTarget(baseCombatEntity) && turret.InFiringArc(baseCombatEntity) && turret.ObjectVisible(baseCombatEntity))
                                {
                                    turret.SetTarget(baseCombatEntity);
                                    if (turret.target != null)
                                        break;
                                }
                            }
                        }
                    }
                    if (!turret.PeacekeeperMode() || !((UnityEngine.Object)turret.target == (UnityEngine.Object)null) || turret.IsBeingControlled || this.IsBeingControlled)
                        return;
                        turret.nextShotTime = UnityEngine.Time.time + 1f;
                }
            }

            public virtual bool Ignore(BasePlayer player) => false;

            public void TargetTick()
            {
                if ((double)UnityEngine.Time.realtimeSinceStartup >= (double)turret.nextVisCheck)
                {
                    turret.nextVisCheck = UnityEngine.Time.realtimeSinceStartup + UnityEngine.Random.Range(0.2f, 0.3f);
                    turret.targetVisible = turret.ObjectVisible(turret.target);
                    if (turret.targetVisible)
                        turret.lastTargetSeenTime = UnityEngine.Time.realtimeSinceStartup;
                }
                turret.EnsureReloaded();

                BaseProjectile attachedWeapon = turret.GetAttachedWeapon();
                if ((double)UnityEngine.Time.time >= (double)turret.nextShotTime && turret.targetVisible && (double)Mathf.Abs(turret.AngleToTarget(turret.target, (double)turret.currentAmmoGravity != 0.0)) < (double)turret.GetMaxAngleForEngagement())
                {
                    if ((bool)(UnityEngine.Object)attachedWeapon)
                    {
                        if (!_.configData.settings.amoRequired)
                            attachedWeapon.primaryMagazine.contents = 100;

                        if (attachedWeapon.primaryMagazine.contents > 0)
                        {
                            FireAttachedGun(turret.AimOffset(turret.target), turret.aimCone, target: (turret.PeacekeeperMode() ? turret.target : (BaseCombatEntity)null));
                            float delay = attachedWeapon.isSemiAuto ? attachedWeapon.repeatDelay * 1.5f : attachedWeapon.repeatDelay;
                            turret.nextShotTime = UnityEngine.Time.time + attachedWeapon.ScaleRepeatDelay(delay);
                        }
                        else
                            turret.nextShotTime = UnityEngine.Time.time + 5f;
                    }
                }
                if (!((UnityEngine.Object)turret.target == (UnityEngine.Object)null) && !turret.target.IsDead() && ((double)UnityEngine.Time.realtimeSinceStartup - (double)turret.lastTargetSeenTime <= 3.0 && (double)Vector3.Distance(turret.transform.position, turret.target.transform.position) <= (double)turret.sightRange) && (!turret.PeacekeeperMode() || turret.IsEntityHostile(turret.target)))
                    return;
                turret.SetTarget((BaseCombatEntity)null);
            }

            public void FireAttachedGun(Vector3 targetPos, float aimCone, Transform muzzleToUse = null, BaseCombatEntity target = null)
            {
                BaseProjectile attachedWeapon = turret.GetAttachedWeapon();
                if ((UnityEngine.Object)attachedWeapon == (UnityEngine.Object)null || turret.IsOffline())
                    return;
                Transform pitch = turret.gun_pitch;
                attachedWeapon.aiAimCone = 0f;
                attachedWeapon.ServerUse(1f, 1f, turret.IsBeingControlled ? turret.RCEyes : pitch);
                if (!attachedWeapon.name.Contains("minigun"))
                    turret.ClientRPC<uint, Vector3>((Network.Connection)null, "CLIENT_FireGun", StringPool.Get(turret.muzzlePos.gameObject.name), targetPos);
            }

            public bool isAuthedPlayer(BasePlayer player) => turret.IsAuthed(player);
            public bool HasInterference() => turret.IsOnFire();

            private void HavePowerStill()
            {
                bool online = turret.IsOnline();
                float enegry = button.GetCurrentEnergy();

                if (enegry < requiredPower && online || online && !button.IsOn())
                {
                    turret.SetIsOnline(false);
                    Effect.server.Run(turret.offlineSound.resourcePath, (BaseEntity)turret, 0U, Vector3.zero, Vector3.zero);
                }
                else if (enegry >= requiredPower && !online && button.IsOn())
                {
                    turret.SetIsOnline(true);
                    Effect.server.Run(turret.onlineSound.resourcePath, (BaseEntity)turret, 0U, Vector3.zero, Vector3.zero);
                }

            }

            public static int GetNearbyTurrets(AutoTurret newTurret, float radius = 40)
            {
                List<NPCAutoTurret> list = Facepunch.Pool.Get<List<NPCAutoTurret>>();
                Vis.Entities<NPCAutoTurret>(newTurret.transform.position, radius, list, 256, QueryTriggerInteraction.Ignore);
                int count = list.Count;
                Facepunch.Pool.FreeUnmanaged<NPCAutoTurret>(ref list);
                return count;
            }

            private void runHookCalls()
            {
                if (OwnerPlayer != null)
                {
                    _.turretLoadoutsFill(OwnerPlayer, turret);
                }
            }

            public void CreateInventory()
            {
                if (turret.socketTransform == null)
                    turret.socketTransform = turret.muzzlePos;
                turret.inventory.entityOwner = (BaseEntity)turret;
                turret.inventory.capacity = 7;
                turret.inventory.canAcceptItem += new Func<Item, int, bool>(CanAcceptItem);
                turret.inventory.onItemAddedRemoved = new Action<Item, bool>(OnItemAddedOrRemoved);
                turret.dropsLoot = false;
                turret.lootPanelName = lootname;
                turret.baseProtection = newProtection;
                turret.baseProtection.amounts = protectionSettings;

                if (!_.configData.settings.gunRequired)
                    Invoke("AddGunAndLockSlot", 1.1f);
                if (!_.configData.settings.gunRequired && !_.configData.settings.amoRequired)
                    turret.inventory.SetLocked(true);
            }

            public bool CanAcceptItem(Item item, int targetSlot)
            {
                Item slot = turret.inventory.GetSlot(0);

                if (turret.IsValidWeapon(item) && targetSlot == 0)
                    return true;

                if (turret.IsValidWeapon(item) && targetSlot == 0)
                    return true;

                if (isSamSiteAlso && item.info.itemid == -384243979 && targetSlot != 0)
                    return true;

                if (item.info.category != ItemCategory.Ammunition)
                    return false;
                ItemModProjectile component = item.info.GetComponent<ItemModProjectile>();
                BaseProjectile attachedWeapon = turret.GetAttachedWeapon();
                return slot != null && !((UnityEngine.Object)attachedWeapon == (UnityEngine.Object)null) && (!((UnityEngine.Object)component == (UnityEngine.Object)null) && (attachedWeapon.primaryMagazine.definition.ammoTypes & component.ammoType) != (AmmoTypes)0) && targetSlot != 0;
            }

            public void OnItemAddedOrRemoved(Item item, bool added)
            {
                if (!added)
                {
                    BaseProjectile attachedWeapon = item.GetHeldEntity() as BaseProjectile;
                    if (attachedWeapon != null)
                    {
                        if (!attachedWeapon.name.Contains("minigun"))
                            attachedWeapon.SetFlag(BaseEntity.Flags.Disabled, false);
                        heldItem = attachedWeapon;
                    }
                }
                if (samMod != null)
                    samMod.EnsureReloaded();
                if (!(bool)(UnityEngine.Object)item.info.GetComponent<ItemModEntity>())
                    return;
                if (turret.IsInvoking(new Action(UpdateAttachedWeapon)))
                    UpdateAttachedWeapon();
                turret.Invoke(new Action(UpdateAttachedWeapon), 0.5f);
            }

            public void UpdateAttachedWeapon()
            {
                HeldEntity turretNew = TryAddWeaponToTurret(turret.inventory.GetSlot(0), turret.socketTransform, (BaseEntity)turret, turret.attachedWeaponZOffsetScale);
                bool b = (UnityEngine.Object)turretNew != (UnityEngine.Object)null;
                turret.SetFlag(BaseEntity.Flags.Reserved3, b);
                if (b)
                {
                    BaseProjectile attachedWeaponMod = turret.GetAttachedWeapon();
                    turret.AttachedWeapon = turretNew;
                    turret.totalAmmoDirty = true;
                    turret.Reload();
                    turret.UpdateTotalAmmo();
                    if (attachedWeaponMod != null)
                    {
                        if (!OldCone.ContainsKey(attachedWeaponMod.name))
                            OldCone.Add(attachedWeaponMod.name, attachedWeaponMod.aimCone);

                        attachedWeaponMod.aimCone = _.configData.settings.aimCone;
                    }
                    if (!turret.IsOffline())
                        return;
                    turretNew.SetLightsOn(false);
                }
                else
                {
                    BaseProjectile attachedWeapon = turret.GetAttachedWeapon();
                    if ((UnityEngine.Object)attachedWeapon != (UnityEngine.Object)null)
                    {
                        attachedWeapon.SetGenericVisible(false);
                        attachedWeapon.SetLightsOn(false);

                        if (!OldCone.ContainsKey(attachedWeapon.name))
                            OldCone.Add(attachedWeapon.name, attachedWeapon.aimCone);

                        attachedWeapon.aimCone = _.configData.settings.aimCone;
                    }
                    turret.AttachedWeapon = (HeldEntity)null;
                }
            }

            public static HeldEntity TryAddWeaponToTurret(
              Item weaponItem,
              Transform parent,
              BaseEntity entityParent,
              float zOffsetScale)
            {
                Vector3 gunOffset = new Vector3(-0.0f, -0.036f, -0.30f);

                HeldEntity heldEntity1 = (HeldEntity)null;
                if (weaponItem != null && (weaponItem.info.category == ItemCategory.Weapon || weaponItem.info.category == ItemCategory.Fun))
                {
                    BaseEntity heldEntity2 = weaponItem.GetHeldEntity();
                    if ((UnityEngine.Object)heldEntity2 != (UnityEngine.Object)null)
                    {
                        HeldEntity component = heldEntity2.GetComponent<HeldEntity>();
                        if ((UnityEngine.Object)component != (UnityEngine.Object)null && component.IsUsableByTurret)
                            heldEntity1 = component;
                    }
                }
                if ((UnityEngine.Object)heldEntity1 == (UnityEngine.Object)null)
                    return (HeldEntity)null;
                Transform transform = heldEntity1.transform;
                Transform muzzleTransform = heldEntity1.MuzzleTransform;
                heldEntity1.SetParent((BaseEntity)null);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                Quaternion quaternion = transform.rotation * Quaternion.Inverse(muzzleTransform.rotation);
                heldEntity1.limitNetworking = false;
                if (!heldEntity1.name.Contains("minigun"))
                    heldEntity1.SetFlag(BaseEntity.Flags.Disabled, false);
                heldEntity1.SetParent(entityParent, StringPool.Get(parent.name));
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.rotation *= quaternion;
                Vector3 vector3 = parent.InverseTransformPoint(muzzleTransform.position);
                transform.localPosition = Vector3.left * vector3.x;
                float num = Vector3.Distance(muzzleTransform.position, transform.position);
                transform.localPosition += (Vector3.forward * num * zOffsetScale) + gunOffset;

                heldEntity1.SetGenericVisible(true);
                heldEntity1.SetLightsOn(true);
                if (!heldEntity1.name.Contains("minigun"))
                    heldEntity1.SetFlag(BaseEntity.Flags.Disabled, true);

                if (_ != null)
                {
                    _.NextTick(() =>
                    {
                        if (heldEntity1 != null && !heldEntity1.name.Contains("minigun"))
                            heldEntity1.SetFlag(BaseEntity.Flags.Disabled, true);
                        heldEntity1?.SendNetworkUpdate();
                    });
                }
                return heldEntity1;
            }

            private ElectricSwitch AddOrGetButton()
            {
                ElectricSwitch buttonFind = turret.GetComponentInChildren<ElectricSwitch>();
                if (buttonFind == null)
                {
                    ElectricSwitch buttonNew = GameManager.server.CreateEntity(switchPrefabString, position) as ElectricSwitch;
                    if (buttonNew == null)
                        return null;

                    buttonNew.Spawn();
                    buttonNew.SetParent(turret, true);
                    button = buttonNew;
                }
                else
                {
                    button = buttonFind;
                }

                button.transform.localPosition = new Vector3(-0.76f, 0.09f, -0.54f);
                button.transform.localRotation = Quaternion.Euler(new Vector3(-55, -140f, 0f));
                button.InitializeHealth(10 * 10000, 10 * 10000);
                button.pickup.enabled = false;
                UnityEngine.Object.Destroy(button.GetComponent<DestroyOnGroundMissing>());
                UnityEngine.Object.Destroy(button.GetComponent<GroundWatch>());

                IOEntity.IOSlot ioOutput = button.outputs[0];
                ioOutput.type = IOEntity.IOType.Generic;
                button.SendNetworkUpdateImmediate(true);

                return button;
            }

            public void onSwitchToggle()
            {
                if (!button.IsOn())
                {
                    if (button.GetCurrentEnergy() >= requiredPower)
                    {
                        turret.SetIsOnline(true);
                        Effect.server.Run(turret.onlineSound.resourcePath, (BaseEntity)turret, 0U, Vector3.zero, Vector3.zero);
                    }
                }
                else
                {
                    turret.SetIsOnline(false);
                    Effect.server.Run(turret.offlineSound.resourcePath, (BaseEntity)turret, 0U, Vector3.zero, Vector3.zero);
                }
            }

            bool hasResetGun = false;

            public void dropLoot()
            {
                if (gunItem != null)
                {  
                    BaseProjectile heldEntity = gunItem?.GetHeldEntity()?.GetComponent<BaseProjectile>();
                    if (heldEntity != null && _.configData.settings.gunRequired && !hasResetGun && !heldEntity.name.Contains("minigun"))
                    {
                        heldEntity.SetGenericVisible(false);
                        heldEntity.SetLightsOn(false);
                        heldEntity.SetFlag(BaseEntity.Flags.Disabled, false);
                        hasResetGun = true;
                    }
                }

                if (_.configData.settings.gunRequired && turret.AttachedWeapon != null)
                    turret.AttachedWeapon.SetFlag(BaseEntity.Flags.Disabled, false);

                if (turret.inventory == null || turret.inventory.itemList.Count <= 0)
                    return;

                DroppedItemContainer containerDrop = ItemContainer.Drop("assets/prefabs/misc/item drop/item_drop.prefab", turret.transform.position + new Vector3(0, 2f, 0), turret.transform.rotation, turret.inventory);

                if (!hasResetGun && containerDrop != null)
                {
                    foreach (var obj in containerDrop.inventory.itemList)
                    {
                        if (obj != null)
                        {
                            if (obj.skin == 3197480971)
                            {
                                if (!_.configData.settings.gunRequired && _.configData.settings.amoRequired)
                                    unloadAmmo(containerDrop);

                                obj.UseItem(1);
                                continue;
                            }

                            BaseProjectile attachedWeapon = obj.GetHeldEntity()?.GetComponent<BaseProjectile>();

                            if ((UnityEngine.Object)attachedWeapon != (UnityEngine.Object)null)
                            {
                                attachedWeapon.SetGenericVisible(false);
                                attachedWeapon.SetLightsOn(false);
                                if (!attachedWeapon.name.Contains("minigun"))
                                {
                                    attachedWeapon.SetFlag(BaseEntity.Flags.Disabled, false);
                                }
                            }
                        }
                    }
                }
            }

            private void unloadAmmo(DroppedItemContainer containerDrop = null)
            {
                if (!_.configData.settings.amoRequired)
                    return;
                ItemContainer inventory = turret.inventory;
                if (containerDrop != null)
                    inventory = containerDrop.inventory;

                BaseProjectile component = gunItem?.GetHeldEntity()?.GetComponent<BaseProjectile>();
                if (component == null)
                    return;
                int contents = component.primaryMagazine.contents;
                if (contents <= 0)
                    return;
                component.SetAmmoCount(0);
                gunItem.MarkDirty();
                component.SendNetworkUpdateImmediate();
                Item obj = ItemManager.Create(component.primaryMagazine.ammoType, contents);

                if (!obj.MoveToContainer(inventory, -1, true, true, null, true))
                {
                    Vector3 velocity = Vector3.zero;
                    obj.Drop(turret.transform.position + new Vector3(0.5f, 1f, 0), velocity);
                }
            }

            public void RestoreGun()
            {
                HeldEntity component = gunItem?.GetHeldEntity()?.GetComponent<HeldEntity>();
                if (component != null)
                {
                    if (!component.name.Contains("minigun"))
                        component.SetFlag(BaseEntity.Flags.Disabled, false);
                }

                BaseProjectile attachedWeapon = turret?.GetAttachedWeapon();

                if (attachedWeapon != null)
                {
                    if (!attachedWeapon.name.Contains("minigun"))
                        attachedWeapon.SetFlag(BaseEntity.Flags.Disabled, false);

                    if (OldCone.ContainsKey(attachedWeapon.name))
                        attachedWeapon.aimCone = OldCone[attachedWeapon.name];
                }
            }
            public void OnDestroy()
            {
                if (destroyran)
                    return;

                destroyran = true;

                if (_ != null && _.configData.settings.powerRequirementNeeded)
                    button?.UpdateFromInput(0, 0);

                if (_ != null && turret != null && !turret.IsDestroyed)
                {
                    if (!_.pcdData.Health.ContainsKey(turret.net.ID.Value))
                    {
                        _.pcdData.Health.Add(turret.net.ID.Value, turret.health);
                    }
                }

                if (uiMono != null)
                    UnityEngine.Object.Destroy(uiMono);

                if (samMod != null)
                    UnityEngine.Object.Destroy(samMod);

                turret.CancelInvoke(new Action(ServerTick));
                turret.SendNetworkUpdate();

                RestoreGun();

                if (!turret.IsDestroyed)
                {
                    turret.SetIsOnline(false);
                    if (!_.configData.settings.gunRequired && gunItem != null)
                    {
                        unloadAmmo();
                        if (gunItem.skin == 3197480971)
                            gunItem.UseItem(1);
                    }
                }
                else
                {
                    if (!isInPickUp)
                    {
                        if (uiMono == null || !uiMono.pickup)
                            SendEffect("assets/bundled/prefabs/fx/entities/loot_barrel/gib.prefab", turret.transform.position, (BaseEntity)turret);
                    }
                    dropLoot();
                }

                if (!_.configData.settings.gunRequired && !_.configData.settings.amoRequired)
                    turret.inventory.SetLocked(false);
            }
        }
        #endregion

        #region CCTV_RC
        [AutoPatch]
        [HarmonyPatch(typeof(Drone), nameof(Drone.UserInput))]
        internal class Patch
        {
            [HarmonyPrefix]
            internal static bool Prefix(Drone __instance, InputState inputState, CameraViewerId viewerID)
            {
                if ((object)__instance == null || !_allCCTV.TryGetValue(__instance, out TurretModInfo controler))
                    return true;

                __instance.SendNetworkUpdateImmediate();                     

                if (controler != null && inputState != null && controler.mod.turret != null)
                {
                    AutoTurret turret = controler.mod.turret;

                    turret.UpdateManualAim(inputState);

                    if (inputState.IsDown(BUTTON.FIRE_PRIMARY))
                    {
                        BaseProjectile attachedWeapon = turret.GetAttachedWeapon();
                        if ((bool)(UnityEngine.Object)attachedWeapon)
                        {
                            if (attachedWeapon.primaryMagazine.contents > 0)
                            {
                                if (turret.nextShotTime < UnityEngine.Time.time)
                                {
                                    Vector3 origin = turret.GetCenterMuzzle().transform.position - turret.GetCenterMuzzle().forward * 0.25f;
                                    Vector3 vector3 = turret.GetCenterMuzzle().transform.forward;
                                    Vector3 aimConeDirection = AimConeUtil.GetModifiedAimConeDirection(turret.aimCone, vector3);
                                    Vector3 targetPos = origin + aimConeDirection * 300f;

                                    controler.mod.FireAttachedGun(origin, turret.aimCone);
                                    float delay = attachedWeapon.isSemiAuto ? attachedWeapon.repeatDelay * 1.5f : attachedWeapon.repeatDelay;
                                    turret.nextShotTime = UnityEngine.Time.time + attachedWeapon.ScaleRepeatDelay(delay);
                                    if (controler.player != null)
                                        controler.player.MarkHostileFor(60f);
                                }
                            }
                            else
                            {
                                turret.nextShotTime = UnityEngine.Time.time + 2f;
                                turret.Reload();
                            }
                        }
                        else if (turret.HasGenericFireable())
                        {
                            turret.AttachedWeapon.ServerUse();
                            turret.nextShotTime = UnityEngine.Time.time + 0.115f;
                            if (controler.player != null)
                                controler.player.MarkHostileFor(60f);
                        }
                        else
                            turret.nextShotTime = UnityEngine.Time.time + 1f;
                    }
                }
                return false;
            }
        }
        #endregion

        #region SamMod
        private class SamMod : MonoBehaviour
        {
            private AutoTurret turret { get; set; }
            private Vector3 position { get; set; }
            public SamSite.ISamSiteTarget currentTarget;
            public SamSite.SamTargetType mostRecentTargetType;
            public Item ammoItem;
            public float lockOnTime;
            public float lastTargetVisibleTime;
            public int lastAmmoCount;
            public int currentTubeIndex;
            public int firedCount;
            public float nextBurstTime;
            public int lowAmmoThreshold = 5;
            public bool HasValidTarget() => !currentTarget.IsUnityNull<SamSite.ISamSiteTarget>() || validPatrolHelicopterTarget();
            public Vector3 currentAimDir = Vector3.forward;
            public Vector3 targetAimDir = Vector3.forward;
            public SphereCollider col { get; set; }
            private Item gunItem { get; set; }
            private bool canTargetAIHelicopter { get; set; }
            private PatrolHelicopter patrolHelicopterTarget { get; set; }

            public void ClearTarget()
            {
                SetTarget((SamSite.ISamSiteTarget)null);
                if (patrolHelicopterTarget != null)
                    patrolHelicopterTarget = null;
            }

            private bool validPatrolHelicopterTarget()
            {
                if (patrolHelicopterTarget != null && !patrolHelicopterTarget.IsDestroyed)
                    return true;
                return false;
            }
            private void Awake()
            {
                turret = GetComponent<AutoTurret>();
                position = turret.transform.position;
                canTargetAIHelicopter = _.configData.settings.isAl1owedPatrol;

                turret.InvokeRandomized(new Action(TargetScan), 1f, 3f, 1f);
            }

            private void Update()
            {
                if (turret.IsOffline())
                    return;

                if (!HasValidTarget() || turret.IsDead() || turret.HasTarget())
                    return;

                if (validPatrolHelicopterTarget())
                {
                    var targetVelocity = patrolHelicopterTarget.myAI.GetLastMoveDir() * patrolHelicopterTarget.myAI.GetMoveSpeed() * 1.25f; ;
                    var estimatedPoint = PredictedPos(patrolHelicopterTarget, turret, targetVelocity, 1);

                    turret.aimDir = (estimatedPoint - turret.eyePos.transform.position).normalized;
                }
                else if (currentTarget != null && turret != null && currentTarget.CenterPoint() != null)
                {
                    var targetComponent = currentTarget as BaseEntity;
                    if (targetComponent == null)
                        return;

                    var targetVelocity = currentTarget.GetWorldVelocity();
                    var estimatedPoint = PredictedPos(targetComponent, turret, targetVelocity, currentTarget.SAMTargetType.speedMultiplier);
                    turret.aimDir = (estimatedPoint - turret.eyePos.transform.position).normalized;
                }
            }

            private void AddMLRSRockets(List<SamSite.ISamSiteTarget> allTargets, float scanRadius)
            {
                if (MLRSRocket.serverList.Count == 0)
                    return;
                foreach (MLRSRocket server in MLRSRocket.serverList)
                {
                    if ((double)Vector3.Distance(server.transform.position, this.transform.position) < (double)scanRadius)
                        allTargets.Add((SamSite.ISamSiteTarget)server);
                }
            }

            public void TargetScan()
            {
                if (turret == null)
                    turret = this.GetComponent<AutoTurret>();

                if (turret == null)
                    return;

                if (turret.IsOffline())
                {
                    lastTargetVisibleTime = 0.0f;
                }
                else
                {
                    if ((double)UnityEngine.Time.time > (double)lastTargetVisibleTime + 3.0)
                        ClearTarget();

                    int num = ammoItem == null || ammoItem.parent != turret.inventory ? 0 : ammoItem.amount;
                    bool flag1 = lastAmmoCount < lowAmmoThreshold;
                    bool flag2 = num < lowAmmoThreshold;
                    lastAmmoCount = num;

                    if (turret.HasClipAmmo() && turret.HasTarget())
                    {
                        ClearTarget();
                        return;
                    }

                    if (HasValidTarget() || turret.IsDead())
                        return;

                    if (SamSite.targetTypeVehicle?.scanRadius == null)
                    {
                        SamSite.targetTypeUnknown = new SamSite.SamTargetType(150, 1f, 5f);
                        SamSite.targetTypeMissile = new SamSite.SamTargetType(150, 2.25f, 3.5f);
                        SamSite.targetTypeVehicle = new SamSite.SamTargetType(150, 1f, 5f);
                    }
                    List<SamSite.ISamSiteTarget> list = Facepunch.Pool.Get<List<SamSite.ISamSiteTarget>>();
                    AddTargetSet(list, 32768, SamSite.targetTypeVehicle.scanRadius);
                    AddMLRSRockets(list, 400);

                    SamSite.ISamSiteTarget target = (SamSite.ISamSiteTarget)null;

                    foreach (SamSite.ISamSiteTarget samSiteTarget in list)
                    {
                        if (samSiteTarget == null)
                            continue;

                        var mountPoints = (samSiteTarget as BaseVehicle)?.mountPoints;
                        if (mountPoints != null && IsOccupiedByAuthed(turret, mountPoints))
                            continue;

                        if (samSiteTarget.SAMTargetType == SamSite.targetTypeMissile)
                        {
                            target = samSiteTarget;
                            break;
                        }

                        if (!samSiteTarget.isClient && (double)samSiteTarget.CenterPoint().y >= (double)turret.eyePos.transform.position.y && (samSiteTarget.IsVisible(turret.eyePos.transform.position, samSiteTarget.SAMTargetType.scanRadius * 2f) && samSiteTarget.IsValidSAMTarget(false)) && Interface.CallHook("OnSamSiteTarget", (object)turret, (object)samSiteTarget) == null)
                        {
                            target = samSiteTarget;
                            break;
                        }
                    }

                    if (canTargetAIHelicopter)
                    {
                        var samSitePosition = position;
                        patrolHelicopterTarget = null;

                        if (_allPatrolHelicopters.Count > 0)
                        {
                            foreach (var targetComponent in _allPatrolHelicopters)
                            {
                                if (targetComponent == null || targetComponent.IsDestroyed || targetComponent.transform == null)
                                    continue;

                                if (Vector3.Distance(samSitePosition, targetComponent.transform.position) <= 150)
                                {
                                    patrolHelicopterTarget = targetComponent;
                                    break;
                                }
                            }
                        }
                    }

                    if ((!target.IsUnityNull<SamSite.ISamSiteTarget>() && currentTarget != target) || validPatrolHelicopterTarget())
                        lockOnTime = UnityEngine.Time.time + 0.5f;

                    SetTarget(target);

                    if ((currentTarget != null && !currentTarget.IsUnityNull<SamSite.ISamSiteTarget>()) || validPatrolHelicopterTarget())
                        lastTargetVisibleTime = UnityEngine.Time.time;
                    Facepunch.Pool.FreeUnmanaged<SamSite.ISamSiteTarget>(ref list);
                    if ((currentTarget != null && currentTarget.IsUnityNull<SamSite.ISamSiteTarget>() && (!validPatrolHelicopterTarget() && currentTarget.SAMTargetType != SamSite.targetTypeMissile)))
                        turret.CancelInvoke(new Action(WeaponTick));
                    else
                    {
                        if (currentTarget != null && currentTarget.SAMTargetType == SamSite.targetTypeMissile)
                        {
                            lockOnTime = UnityEngine.Time.time + 0.2f;
                            turret.InvokeRandomized(new Action(WeaponTick), 0.1f, 0.3f, 0.2f);
                        }
                        else
                            turret.InvokeRandomized(new Action(WeaponTick), 0.0f, 0.5f, 0.2f);
                    }
                }

                void AddTargetSet(List<SamSite.ISamSiteTarget> allTargets, int layerMask, float scanRadius)
                {
                    List<SamSite.ISamSiteTarget> list = Facepunch.Pool.Get<List<SamSite.ISamSiteTarget>>();
                    Vis.Entities<SamSite.ISamSiteTarget>(turret.transform.position, scanRadius, list, layerMask, QueryTriggerInteraction.Ignore);
                    allTargets.AddRange((IEnumerable<SamSite.ISamSiteTarget>)list);
                    Facepunch.Pool.FreeUnmanaged<SamSite.ISamSiteTarget>(ref list);
                }
            }

            public void SetTarget(SamSite.ISamSiteTarget target)
            {
                int num = currentTarget != target ? 1 : 0;
                currentTarget = target;
                if (!target.IsUnityNull<SamSite.ISamSiteTarget>())
                    mostRecentTargetType = target.SAMTargetType;
                if (num == 0)
                    return;
            }

            public void WeaponTick()
            {
                if (turret.HasTarget() || !HasValidTarget() || turret.IsDead() || (double)UnityEngine.Time.time < (double)lockOnTime || (double)UnityEngine.Time.time < (double)nextBurstTime)
                    return;

                if (turret.IsOffline())
                    firedCount = 0;

                else if (firedCount >= 4)
                {
                    if (mostRecentTargetType == SamSite.targetTypeMissile)
                        nextBurstTime = UnityEngine.Time.time;
                    else if (currentTarget == null)
                        nextBurstTime = UnityEngine.Time.time + 3f;
                    else
                        nextBurstTime = UnityEngine.Time.time + mostRecentTargetType.timeBetweenBursts;
                    firedCount = 0;
                }
                else
                {
                    EnsureReloaded();
                    if (Interface.CallHook("CanSamTurretShoot", (object)turret) != null || !HasAmmo())
                        return;
                    int num = ammoItem == null ? 0 : (ammoItem.amount == lowAmmoThreshold ? 1 : 0);
                    if (ammoItem != null)
                        ammoItem.UseItem();
                    ++firedCount;
                    float speedMultiplier = 1f;
                    if (currentTarget != null && !currentTarget.IsUnityNull<SamSite.ISamSiteTarget>())
                        speedMultiplier = currentTarget.SAMTargetType.speedMultiplier;

                    FireProjectile(turret.eyePos.transform.position + new Vector3(0.4f, 0, 0), turret.aimDir, speedMultiplier, validPatrolHelicopterTarget());
                    if (num == 0)
                        return;
                }
            }

            private Vector3 PredictedPos(BaseEntity target, AutoTurret samSite, Vector3 targetVelocity, float projectileSpeedMultiplier)
            {
                Vector3 targetpos = target.transform.TransformPoint(target.transform.GetBounds().center);
                Vector3 displacement = targetpos - samSite.eyePos.transform.position;
                float projectileSpeed = 90 * projectileSpeedMultiplier;
                float targetMoveAngle = Vector3.Angle(-displacement, targetVelocity) * Mathf.Deg2Rad;
                if (targetVelocity.magnitude == 0 || targetVelocity.magnitude > projectileSpeed && Mathf.Sin(targetMoveAngle) / projectileSpeed > Mathf.Cos(targetMoveAngle) / targetVelocity.magnitude)
                    return targetpos;

                float shootAngle = Mathf.Asin(Mathf.Sin(targetMoveAngle) * targetVelocity.magnitude / projectileSpeed);
                return targetpos + targetVelocity * displacement.magnitude / Mathf.Sin(Mathf.PI - targetMoveAngle - shootAngle) * Mathf.Sin(shootAngle) / targetVelocity.magnitude;
            }

            public void FireProjectile(Vector3 origin2, Vector3 direction, float speedMultiplier, bool isNonPlayerCopter = false)
            {
                if (isNonPlayerCopter)
                {
                    if (patrolHelicopterTarget == null || patrolHelicopterTarget.CenterPoint() == null || patrolHelicopterTarget.CenterPoint() == Vector3.zero)
                        return;
                }
                else
                {
                    if (currentTarget == null || currentTarget.CenterPoint() == null || currentTarget.CenterPoint() == Vector3.zero)
                        return;
                }

                BaseEntity rocket = null;
                rocket = GameManager.server.CreateEntity($"assets/prefabs/npc/sam_site_turret/rocket_sam.prefab", origin2 + turret.eyePos.transform.forward * 2, Quaternion.LookRotation(direction, Vector3.up));
                if (rocket == null) return;
                var proj = rocket.GetComponent<ServerProjectile>();
                if (proj == null) return;
                rocket.creatorEntity = turret;

                ServerProjectile component = rocket.GetComponent<ServerProjectile>();
                if ((bool)(UnityEngine.Object)component)
                    component.InitializeVelocity(turret.aimDir * 90);
                rocket.Spawn();

            }

            public virtual bool HasAmmo()
            {
                return ammoItem != null && ammoItem.amount > 0 && ammoItem.parent == turret.inventory;
            }

            public void Reload()
            {
                for (int index = 0; index < turret.inventory.itemList.Count; ++index)
                {
                    Item obj = turret.inventory.itemList[index];
                    if (obj != null && obj.info.itemid == -384243979 && obj.amount > 0)
                    {
                        ammoItem = obj;
                        return;
                    }
                }
                ammoItem = (Item)null;
            }

            public void EnsureReloaded()
            {
                if (HasAmmo())
                    return;
                Reload();
            }

            private static bool IsOccupiedByAuthed(AutoTurret entity, List<MountPointInfo> mountPoints)
            {
                int totalFoundPlayers = 0;

                if (mountPoints != null)
                {
                    foreach (var mountPoint in mountPoints)
                    {
                        var player = mountPoint.mountable.GetMounted();
                        if (player != null)
                        {
                            totalFoundPlayers++;
                            if (entity.IsAuthed(player))
                                return true;
                        }
                    }
                }
                else
                    return false;

                if (totalFoundPlayers > 0)
                    return false;

                return true;
            }

            public void OnDestroy()
            {
                turret.CancelInvoke(new Action(TargetScan));
                turret.CancelInvoke(new Action(WeaponTick));
            }
        }
        #endregion

        #region Effects
                private static void SendEffect(string effect, Vector3 position, BaseEntity entity = null)
                {
                    Effect infoEffect = new Effect(effect, null, 0, position, new Vector3());
                    infoEffect.broadcast = true;
                    EffectNetwork.Send(infoEffect);
                }
        #endregion

        #region Localization
                public static void GameTipMessage(BasePlayer player, string message)
                {
                    if (player != null && player.userID.IsSteamId())
                    {
                        player?.SendConsoleCommand("gametip.hidegametip");
                        player?.SendConsoleCommand("gametip.showgametip", message);
                        _.timer.Once(3, () => player?.SendConsoleCommand("gametip.hidegametip"));
                    }
                }

                private new void LoadDefaultMessages()
                {
                    lang.RegisterMessages(new Dictionary<string, string>
                    {
                        ["nope"] = "<color=#ce422b>You lack the perms to use this command!</color>",
                        ["usagecommand"] = "<color=#ce422b>/sentry <total></color>",
                        ["gave"] = "<color=#ce422b>You have just got some Sentry Turret!</color>",
                        ["droped"] = "<color=#ce422b>You'r inventory was full so i dropped your Sentry Turret on the ground!</color>",
                        ["maxplace"] = "You have reached your max placement of {0} in this area!",
                        ["deauth"] = "DeAuth",
                        ["auth"] = "Auth",
                        ["clearauth"] = "Clear Auth",
                        ["flip"] = "Flip",
                        ["attackall"] = "In Attack All",
                        ["peacekeeper"] = "In Peacekeeper",
                        ["open"] = "OPEN",
                        ["authorize"] = "AUTHORIZE",
                        ["pickup"] = "PICKUP",
                        ["identifier"] = "SET IDENTIFIER",
                        ["identifier2"] = "Anyone can access any device if they have the identifier, use a unique name!",
                        ["cancel"] = "CANCEL",
                        ["set"] = "SET"
                    }, this);
                }
        #endregion

        #region CUI
        public class UIMonoSentry : MonoBehaviour
        {
            private AutoTurret turret { get; set; }
            private TurretMod mono { get; set; }
            private readonly HashSet<BasePlayer> triggerPlayers = new HashSet<BasePlayer>();
            private Collider boxcollider { get; set; }
            public List<BasePlayer> uiPlayers = new List<BasePlayer>();
            public Dictionary<BasePlayer, int> hammerPlayer = new Dictionary<BasePlayer, int>();

            public bool pickup { get; set; }
            private ElectricSwitch switchEntity { get; set; }

            private void Awake()
            {
                turret = gameObject.GetComponentInParent<AutoTurret>();
                mono = turret?.GetComponent<TurretMod>();
                turret.SetFlag(BaseEntity.Flags.Busy, true);
                switchEntity = turret.GetComponentInChildren<ElectricSwitch>();
            }

            public static UIMonoSentry AddToEntity(AutoTurret entity)
            {
                var gameObject = entity.gameObject.CreateChild();
                gameObject.layer = (int)Rust.Layer.Trigger;

                UIMonoSentry listener = gameObject.GetOrAddComponent<UIMonoSentry>();

                var collider = gameObject.GetOrAddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.size = new Vector3(3.2f, 3.0f, 3.2f);
                listener.boxcollider = collider;

                return listener;
            }

            private void OnTriggerEnter(Collider collider)
            {
                BasePlayer player = collider.ToBaseEntity() as BasePlayer;

                if (player == null)
                    return;

                triggerPlayers.Add(player);
            }

            private void OnTriggerExit(Collider collider)
            {
                BasePlayer player = collider.ToBaseEntity() as BasePlayer;

                if (player == null)
                    return;

                triggerPlayers.Remove(player);
                DestroyCuiAll(player);
            }

            //this.onlyOneUser
            public static bool OccupiedCheck(BasePlayer player)
            {
                if (player.inventory.loot.entitySource == null)
                    return false;

                return true;
            }
            public bool CanOpenLoot(BasePlayer player) => turret.IsOffline() && turret.IsAuthed(player);

            private void Update()
            {
                if (turret == null)
                    return;

                if (triggerPlayers.Count > 0)
                {
                    float time = UnityEngine.Time.realtimeSinceStartup;

                    foreach (var triggerPlayer in triggerPlayers)
                    {
                        if (triggerPlayer == null || !triggerPlayer.CanBuild())
                            continue;

                        if ((double)Vector3.Distance(turret.transform.position, triggerPlayer.transform.position) >= 50.0)
                        {
                            Destroy(this);
                            ResetUI(triggerPlayer);
                            return;
                        }

                        if (turret.IsOnline())
                        {
                            ResetUI(triggerPlayer);
                            return;
                        }

                        int current = 0;
                        bool seePlayer = CanSee(triggerPlayer);
                        bool IsAuthed = turret.IsAuthed(triggerPlayer);
                        bool hammer = triggerPlayer.IsHoldingEntity<Hammer>();
                        bool isHammerPlayer = hammerPlayer.ContainsKey(triggerPlayer);

                        if (isHammerPlayer)
                            current = hammerPlayer[triggerPlayer];

                        if (hammer && IsAuthed)
                        {
                            if (seePlayer && !turret.IsOpen() && !isHammerPlayer)
                            {
                                hammerPlayer.Add(triggerPlayer, 0);
                                BuildPickupCuiScreen(triggerPlayer);
                            }

                            if (!seePlayer && isHammerPlayer)
                            {
                                hammerPlayer.Remove(triggerPlayer);
                                ResetUI(triggerPlayer);
                                return;
                            }

                            if (triggerPlayer.serverInput.IsDown(BUTTON.USE) && isHammerPlayer)
                            {
                                CreateUiBarPickup(triggerPlayer, current);
                                hammerPlayer[triggerPlayer]++;
                                if (current == 0)
                                    RunEffect("assets/bundled/prefabs/fx/notice/loot.drag.dropsuccess.fx.prefab", triggerPlayer);
                                if (current > 100)
                                {
                                    pickup = true;
                                    if (mono != null)
                                        mono.dropLoot();
                                    _.GiveItemSentry(triggerPlayer);
                                    _.NextTick(() =>turret?.Kill());
                                }
                                return;
                            }
                            else if (isHammerPlayer && current > 0)
                            {
                                hammerPlayer[triggerPlayer] = 0;
                                GuiDestroyBarPickup(triggerPlayer);
                            }
                        }
                        else if (isHammerPlayer)
                        {
                            hammerPlayer.Remove(triggerPlayer);
                            ResetUI(triggerPlayer);
                            return;
                        }

                        if (hammer && CanOpenLoot(triggerPlayer))
                            return;

                        if (inputtextOpen.ContainsKey(triggerPlayer.UserIDString))
                        {
                            if (uiPlayers.Contains(triggerPlayer))
                                ResetUI(triggerPlayer);
                            return;
                        }

                        if (seePlayer && !turret.IsOpen())
                        {
                            if (!uiPlayers.Contains(triggerPlayer))
                            {
                                uiPlayers.Add(triggerPlayer);
                                if (IsAuthed)
                                    BuildOpenCuiScreen(triggerPlayer);
                                else
                                    BuildAuthCuiScreen(triggerPlayer);
                            }
                        }
                        else if (uiPlayers.Contains(triggerPlayer))
                        {
                            ResetUI(triggerPlayer);
                        }

                        if (triggerPlayer.serverInput.IsDown(BUTTON.USE) && uiPlayers.Contains(triggerPlayer))
                        {
                            if (!IsAuthed)
                            {
                                AuthPlayer(triggerPlayer);
                                ResetUI(triggerPlayer);
                            }
                            else if (!OccupiedCheck(triggerPlayer) && CanOpenLoot(triggerPlayer))
                                turret.PlayerOpenLoot(triggerPlayer, turret.lootPanelName);
                        }
                    }
                }
            }

            private static void RunEffect(string prefab, BasePlayer player)
            {
                var effect = new Effect();
                effect.Init(Effect.Type.Generic, player.transform.position, Vector3.zero);
                effect.pooledString = prefab;

                if (player != null)
                    EffectNetwork.Send(effect, player.net.connection);
            }


            private void ResetUI(BasePlayer triggerPlayer)
            {
                DestroyCuiOpen(triggerPlayer);
                DestroyCuiAuth(triggerPlayer);
                DestroyCuiPickup(triggerPlayer);
                GuiDestroyBarPickup(triggerPlayer);

                if (uiPlayers.Contains(triggerPlayer))
                    uiPlayers.Remove(triggerPlayer);
                if (hammerPlayer.ContainsKey(triggerPlayer))
                    hammerPlayer.Remove(triggerPlayer);
            }

            private bool CanSee(BasePlayer player)
            {
                bool canSeeTurret = false;
                bool canSeeSwitch = false;
                Quaternion currentRot;
                TryGetPlayerView(player, out currentRot);
                var hitpoints = UnityEngine.Physics.RaycastAll(new Ray(player.transform.position + new Vector3(0f, 0.5f, 0f), currentRot * Vector3.forward), 1f);
                Array.Sort(hitpoints, (a, b) => a.distance == b.distance ? 0 : a.distance > b.distance ? 1 : -1);
                for (var i = 0; i < hitpoints.Length; i++)
                {
                    var entity = hitpoints[i].collider.GetComponentInParent<BaseEntity>();
                    if (entity != null)
                    {
                        if (entity == turret)
                            canSeeTurret = true;
                        if (entity is ElectricSwitch)
                            canSeeSwitch = true;

                        var headDirection = player.eyes.HeadForward();
                        var actualDirection = switchEntity.CenterPoint() - player.eyes.position;

                        var angle = Vector3.Angle(headDirection, actualDirection);
                        if (angle < 10f)
                            canSeeSwitch = true;

                        actualDirection = turret.CenterPoint() - player.eyes.position;
                        angle = Vector3.Angle(headDirection, actualDirection);
                        if (angle < 25f)
                            canSeeTurret = true;

                    }
                }
                return canSeeTurret && !canSeeSwitch;
            }

            private bool TryGetPlayerView(BasePlayer player, out Quaternion viewAngle)
            {
                viewAngle = new Quaternion(0f, 0f, 0f, 0f);
                if (player.serverInput?.current == null) return false;
                viewAngle = Quaternion.Euler(player.serverInput.current.aimAngles);
                return true;

            }

            private void AuthPlayer(BasePlayer player)
            {
                turret.authorizedPlayers.Add(new PlayerNameID
                {
                    userid = player.userID,
                    username = player.displayName
                });

                turret.authDirty = true;
                turret.UpdateMaxAuthCapacity();
                turret.SendNetworkUpdate();
            }

            private void OnDestroy()
            {
                CancelInvoke("CheckInput");

                foreach (var player in triggerPlayers)
                {
                    if (player != null)
                    {
                        DestroyCuiAll(player);
                    }
                }
            }
        }
        #region UI
        private CuiButton createButton(string text, string colorText, TextAnchor align, string colorButton, string command, string ancorMin, string ancorMax, int textSize = 12, string font = "robotocondensed-regular.ttf", string close = "")
        {
            var button = new CuiButton
            {
                Text = { Text = text, FontSize = textSize, Color = colorText, Align = align, Font = font },
                Button = { Color = colorButton, Command = command, Close = close },
                RectTransform = { AnchorMin = ancorMin, AnchorMax = ancorMax }
            };
            return button;
        }

        private CuiElement createElement(string parent, string png, string ancorMin, string ancorMax, string color)
        {
            var element = new CuiElement
            {
                Parent = parent,
                Components = { new CuiImageComponent { Sprite = png, Color = color }, new CuiRectTransformComponent { AnchorMin = ancorMin, AnchorMax = ancorMax } }
            };
            return element;
        }

        public static void DestroyCuiAll(BasePlayer player)
        {
            DestroyCuiOpen(player);
            DestroyCui(player);
            DestroyCuiAuth(player);
            DestroyCuiPickup(player);
            GuiDestroyBarPickup(player);
        }

        public static void DestroyCui(BasePlayer player)
        {
            if (player != null)
            {
                CuiHelper.DestroyUi(player, "Sentry.MainPanel");
                CuiHelper.DestroyUi(player, "Sentry.MainPanelIdent");
                if (inputtextOpen.ContainsKey(player.UserIDString))
                    inputtextOpen.Remove(player.UserIDString);
            }
        }

        private void BuildCuiIdent(BasePlayer player, NPCAutoTurret turret, string identID)
        {
            player.EndLooting();
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Incapacitated, true);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, true);
            player.SendNetworkUpdateImmediate();
            timer.Once(0.05f, () =>
            {
                player?.SetPlayerFlag(BasePlayer.PlayerFlags.Wounded, false);
                player?.SetPlayerFlag(BasePlayer.PlayerFlags.Incapacitated, false);
                player?.SendNetworkUpdateImmediate();
            });

            inputtextOpen[player.UserIDString] = "";
            DestroyCuiOpen(player);

            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0.9" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
            }, "Overlay", "Sentry.MainPanelIdent");


            container.Add(new CuiLabel
            {
                Text = { Text = $"<B>{lang.GetMessage("identifier", this, player.UserIDString)}</B>", FontSize = 40, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.9", },
                RectTransform = { AnchorMin = "0 0.6", AnchorMax = "1 0.7" }
            }, "Sentry.MainPanelIdent");

            container.Add(new CuiLabel
            {
                Text = { Text = $"<B>{lang.GetMessage("identifier2", this, player.UserIDString)}</B>", FontSize = 15, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.9", },
                RectTransform = { AnchorMin = "0 0.59", AnchorMax = "1 0.62" }
            }, "Sentry.MainPanelIdent");

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 0.7"},
                RectTransform = { AnchorMin = "0.42 0.54", AnchorMax = "0.58 0.57" }
            }, "Sentry.MainPanelIdent", "Sentry.MainPanelIdentText");

            // Input Field
            container.Add(new CuiElement
            {
                Name = "Sentry.MainPanelIdentInputReasionData",
                Parent = "Sentry.MainPanelIdentText",
                Components =
                {
                    new CuiInputFieldComponent { NeedsKeyboard = true, Text = identID, CharsLimit = 20, Color = "0 0 0 1", IsPassword = false, Command = "uiinput.sentryturretcommands", Font = "robotocondensed-regular.ttf", FontSize = 17, Align = TextAnchor.MiddleCenter },
                    new CuiRectTransformComponent { AnchorMin = "0.01 0.01", AnchorMax = "0.99 0.99" }
                }
            });

            container.Add(createButton($"<B>{lang.GetMessage("cancel", this, player.UserIDString)}</B>", "1 1 1 1", TextAnchor.MiddleCenter, "0.8 0 0 0.7", $"sentrycommands cancel", $"0.42 0.49", $"0.498 0.53", 16), "Sentry.MainPanelIdent", "Sentry.MainPanel.button55");
            container.Add(createButton($"<B>{lang.GetMessage("set", this, player.UserIDString)}</B>", "1 1 1 1", TextAnchor.MiddleCenter, "0.145 0.255 0.09 0.9", $"sentrycommands set", $"0.502 0.49", $"0.58 0.53", 16), "Sentry.MainPanelIdent", "Sentry.MainPanel.button55");

            CuiHelper.AddUi(player, container);
            lootingPlayer[player.userID] = turret;
        }

        private void BuildCuiScreen(BasePlayer player, NPCAutoTurret turret)
        {
            DestroyCui(player);
            var container = new CuiElementContainer();

            string auth = string.Format(lang.GetMessage("auth", this, player.UserIDString));
            if (turret.IsAuthed(player))
                auth = string.Format(lang.GetMessage("deauth", this, player.UserIDString));

            string mode = string.Format(lang.GetMessage("attackall", this, player.UserIDString));
            if (turret.PeacekeeperMode())
                mode = string.Format(lang.GetMessage("peacekeeper", this, player.UserIDString));

            string rotate = string.Format(lang.GetMessage("flip", this, player.UserIDString));
            string clear = string.Format(lang.GetMessage("clearauth", this, player.UserIDString));
            string rcid = string.Format(lang.GetMessage("identifier", this, player.UserIDString));


            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.8 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "265 295", OffsetMax = "575 340" }
            }, "Inventory", "Sentry.MainPanel");

            container.Add(createButton($"<B>{rcid}</B>", "1 1 1 1", TextAnchor.MiddleCenter, "0.2 0.2 0.2 0.9", $"sentrycommands ident", $"0.30 0.57", $"0.994 1", 13), "Sentry.MainPanel", "Sentry.MainPanel.button0");

            container.Add(createButton($"<B>{auth}</B>", "1 1 1 1", TextAnchor.MiddleCenter, "0.2 0.2 0.2 0.9", $"sentrycommands deauth", $"0 0", $"0.19 0.4", 13), "Sentry.MainPanel", "Sentry.MainPanel.button1");

            container.Add(createButton($"<B>{clear}</B>", "1 1 1 1", TextAnchor.MiddleCenter, "0.2 0.2 0.2 0.9", $"sentrycommands clear", $"0.20 0", $"0.40 0.4", 13), "Sentry.MainPanel", "Sentry.MainPanel.button2");

            container.Add(createButton($"<B>{rotate}</B>", "1 1 1 1", TextAnchor.MiddleCenter, "0.2 0.2 0.2 0.9", $"sentrycommands rotate", $"0.41 0", $"0.61 0.4", 13), "Sentry.MainPanel", "Sentry.MainPanel.button3");

            container.Add(createButton($"<B>{mode}</B>", "1 1 1 1", TextAnchor.MiddleCenter, "0.2 0.2 0.2 0.9", $"sentrycommands mode", $"0.62 0", $"0.994 0.4", 13), "Sentry.MainPanel", "Sentry.MainPanel.button3");

            CuiHelper.AddUi(player, container);
        }

        #region OPEN UI
        public static void DestroyCuiOpen(BasePlayer player)
        {
            if (player != null)
                CuiHelper.DestroyUi(player, "Sentry.MainPanel.Open");
        }

        private static void BuildOpenCuiScreen(BasePlayer player)
        {
            DestroyCuiOpen(player);
            var container = new CuiElementContainer();

            string open = string.Format(_.lang.GetMessage("open", _, player.UserIDString));

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50 20.0", OffsetMax = "50 80.0" }
            }, "Hud", "Sentry.MainPanel.Open");

            container.Add(new CuiLabel
            {
                Text = { Text = $"<B>{open}</B>", FontSize = 16, Align = TextAnchor.LowerCenter, Color = "1 1 1 1", },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, "Sentry.MainPanel.Open", "Sentry.MainPanel.Open.Window");

            container.Add(_.createElement("Sentry.MainPanel.Open", "assets/icons/open.png", "0.35 .3", "0.65 0.8", "1 1 1 1"));

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Ui Auth
        public static void DestroyCuiAuth(BasePlayer player)
        {
            if (player != null)
                CuiHelper.DestroyUi(player, "Sentry.MainPanel.Auth");
        }

        private static void BuildAuthCuiScreen(BasePlayer player)
        {
            DestroyCuiOpen(player);
            DestroyCuiAuth(player);

            var container = new CuiElementContainer();

            string Authorize = string.Format(_.lang.GetMessage("authorize", _, player.UserIDString));

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50 20.0", OffsetMax = "50 80.0" }
            }, "Overlay", "Sentry.MainPanel.Auth");

            container.Add(new CuiLabel
            {
                Text = { Text = $"<B>{Authorize}</B>", FontSize = 16, Align = TextAnchor.LowerCenter, Color = "1 1 1 1", },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, "Sentry.MainPanel.Auth", "Sentry.MainPanel.Auth.Window");

            container.Add(_.createElement("Sentry.MainPanel.Auth", "assets/icons/authorize.png", "0.35 .3", "0.65 0.8", "1 1 1 1"));

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Ui Auth
        public static void DestroyCuiPickup(BasePlayer player)
        {
            if (player != null)
                CuiHelper.DestroyUi(player, "Sentry.MainPanel.Pickup");
        }

        private static void BuildPickupCuiScreen(BasePlayer player)
        {
            DestroyCuiOpen(player);
            DestroyCuiPickup(player);
            var container = new CuiElementContainer();

            string pickup = string.Format(_.lang.GetMessage("pickup", _, player.UserIDString));

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-50 20.0", OffsetMax = "50 80.0" }
            }, "Overlay", "Sentry.MainPanel.Pickup");

            container.Add(new CuiLabel
            {
                Text = { Text = $"<B>{pickup}</B>", FontSize = 16, Align = TextAnchor.LowerCenter, Color = "1 1 1 1", },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, "Sentry.MainPanel.Pickup", "Sentry.MainPanel.Pickup.Window");

            container.Add(_.createElement("Sentry.MainPanel.Pickup", "assets/icons/pickup.png", "0.35 .3", "0.65 0.8", "1 1 1 1"));

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region UI
        private static void GuiDestroyBarPickup(BasePlayer player) => CuiHelper.DestroyUi(player, "Sentry.MainPanel.PickupBar");

        private static void CreateUiBarPickup(BasePlayer player, int current, int total = 100)
        {
            GuiDestroyBarPickup(player);
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                Image = { Color = "0.33 0.33 0.33 0.8" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-70 12.0", OffsetMax = "70 20.0" },
                CursorEnabled = false
            }, "Overlay", "Sentry.MainPanel.PickupBar");

            var mini = 100 * current / total;

            container.Add(new CuiPanel
            {
                Image = { Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.00 0.00", AnchorMax = (mini / 100.0f + " 1") },
                CursorEnabled = false
            }, "Sentry.MainPanel.PickupBar");

            CuiHelper.AddUi(player, container);
        }
        #endregion
        #endregion

        #region UI Commands
        private Dictionary<string, string> inputtext = new Dictionary<string, string>();
        private static Dictionary<string, string> inputtextOpen = new Dictionary<string, string>();
        [ConsoleCommand("uiinput.sentryturretcommands")]
        private void InputTextCallback(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (arg.Args.Length <= 0)
            {
                if (inputtext.ContainsKey(player.UserIDString))
                    inputtext.Remove(player.UserIDString);
                return;
            }
            if (inputtext.ContainsKey(player.UserIDString))
            {
                inputtext[player.UserIDString] = string.Join(" ", arg.Args);
            }
            else
            {
                inputtext.Add(player.UserIDString, string.Join(" ", arg.Args));
            }
        }

        [ConsoleCommand("sentrycommands")]
        private void UiActionCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (player == null || !lootingPlayer.ContainsKey(player.userID))
                return;

            NPCAutoTurret turret = lootingPlayer[player.userID];
            TurretMod mod = turret.GetComponent<TurretMod>();

            if (turret == null)
                return;

            switch (arg.Args[0].ToLower())
            {
                case "deauth":
                    if (turret.booting || turret.IsOnline() || Interface.CallHook("OnTurretDeauthorize", (object)turret, (object)player) != null)
                        return;

                    if (turret.IsAuthed(player))
                    {
                        turret.authorizedPlayers.RemoveWhere((Predicate<PlayerNameID>)(x => x.userid == player.userID));
                    }
                    else
                    {
                        turret.authorizedPlayers.Add(new PlayerNameID
                        {
                            userid = player.userID,
                            username = player.displayName
                        });
                    }

                    turret.authDirty = true;
                    turret.UpdateMaxAuthCapacity();
                    turret.SendNetworkUpdate();
                    BuildCuiScreen(player, turret);
                    return;

                case "clear":
                    if (turret.booting || turret.IsOnline())
                        return;
                    turret.authorizedPlayers.Clear();
                    turret.authDirty = true;
                    turret.UpdateMaxAuthCapacity();
                    turret.SendNetworkUpdate();
                    BuildCuiScreen(player, turret);
                    return;

                case "ident":
                    if (mod == null)
                        return;

                    BuildCuiIdent(player, turret, mod.rcIdentifier);
                    return;

                case "remote":
                    if (!RemoteControlEntity.IDInUse("559122"))
                        turret.rcIdentifier = "559122";
                    turret.SendNetworkUpdate();
                    return;

                case "mode":
                    turret.SetPeacekeepermode(!turret.PeacekeeperMode());
                    BuildCuiScreen(player, turret);
                    return;

                case "cancel":
                    if (!UIMonoSentry.OccupiedCheck(player))
                        turret.PlayerOpenLoot(player, turret.lootPanelName);
                    if (inputtextOpen.ContainsKey(player.UserIDString))
                        inputtextOpen.Remove(player.UserIDString);
                    return;

                case "set":

                    if (!inputtext.TryGetValue(player.UserIDString, out string info))
                        return;

                    if (mod == null)
                        return;

                    mod.UpdateIdentifier(info);

                    if (inputtextOpen.ContainsKey(player.UserIDString))
                        inputtextOpen.Remove(player.UserIDString);

                    if (!UIMonoSentry.OccupiedCheck(player))
                        turret.PlayerOpenLoot(player, turret.lootPanelName);
                    return;

                case "rotate":
                    if (turret.IsOnline() || (turret.booting || Interface.CallHook("OnTurretRotate", (object)turret, (object)player) != null))
                        return;
                    turret.transform.rotation = Quaternion.LookRotation(-turret.transform.forward, turret.transform.up);
                    turret.SendNetworkUpdate();
                    return;

                default:
                    break;
            }
        }
        #endregion
        #endregion
    }
}