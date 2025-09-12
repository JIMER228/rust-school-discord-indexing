using System;
using Facepunch;
using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;
using Rust;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Oxide.Core;
using UnityEngine;
using UnityEngine.AI;
using Oxide.Plugins.NpcSpawnExtensionMethods;

namespace Oxide.Plugins
{
    [Info("NpcSpawn", "KpucTaJl", "2.4.3")]
    internal class NpcSpawn : RustPlugin
    {
        #region Config
        internal class NpcBelt { public string ShortName; public int Amount; public ulong SkinID; public List<string> Mods; public string Ammo; }

        internal class NpcWear { public string ShortName; public ulong SkinID; }

        internal class NpcConfig
        {
            public string Name { get; set; }
            public List<NpcWear> WearItems { get; set; }
            public List<NpcBelt> BeltItems { get; set; }
            public string Kit { get; set; }
            public float Health { get; set; }
            public float RoamRange { get; set; }
            public float ChaseRange { get; set; }
            public float DamageScale { get; set; }
            public float TurretDamageScale { get; set; }
            public float AimConeScale { get; set; }
            public bool DisableRadio { get; set; }
            public bool CanUseWeaponMounted { get; set; }
            public bool CanRunAwayWater { get; set; }
            public float Speed { get; set; }
            public int AreaMask { get; set; }
            public int AgentTypeID { get; set; }
            public string HomePosition { get; set; }
            public HashSet<string> States { get; set; }
            public SensoryStats Sensory { get; set; }
        }

        public class SensoryStats
        {
            public float AttackRangeMultiplier { get; set; }
            public float SenseRange { get; set; }
            public float MemoryDuration { get; set; }
            public bool CheckVisionCone { get; set; }
            public float VisionCone { get; set; }
        }
        #endregion Config

        #region Methods
        private ScientistNPC SpawnNpc(Vector3 position, JObject configJson)
        {
            CustomScientistNpc npc = CreateCustomNpc(position, configJson.ToObject<NpcConfig>());
            if (npc != null)
            {
                _scientists.Add(npc.net.ID, npc);
                npc.skinID = 11162132011012;
            }
            return npc;
        }

        private static CustomScientistNpc CreateCustomNpc(Vector3 position, NpcConfig config)
        {
            ScientistNPC scientistNpc = GameManager.server.CreateEntity("assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_heavy.prefab", position, Quaternion.identity, false) as ScientistNPC;
            ScientistBrain scientistBrain = scientistNpc.GetComponent<ScientistBrain>();

            CustomScientistNpc customScientist = scientistNpc.gameObject.AddComponent<CustomScientistNpc>();
            CustomScientistBrain customScientistBrain = scientistNpc.gameObject.AddComponent<CustomScientistBrain>();

            CopySerializableFields(scientistNpc, customScientist);
            CopySerializableFields(scientistBrain, customScientistBrain);

            UnityEngine.Object.DestroyImmediate(scientistNpc, true);
            UnityEngine.Object.DestroyImmediate(scientistBrain, true);

            customScientist.Config = config;
            customScientist.Brain = customScientistBrain;
            customScientist.enableSaving = false;
            customScientist.gameObject.AwakeFromInstantiate();
            customScientist.Spawn();

            return customScientist;
        }

        private static void CopySerializableFields<T>(T src, T dst)
        {
            FieldInfo[] srcFields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (FieldInfo field in srcFields)
            {
                object value = field.GetValue(src);
                field.SetValue(dst, value);
            }
        }

        private void AddTargetRaid(CustomScientistNpc npc, BuildingPrivlidge cupboard)
        {
            if (npc == null || cupboard == null || !_scientists.ContainsKey(npc.net.ID)) return;
            npc.AddTargetRaid(cupboard);
        }

        private void AddTargetGuard(CustomScientistNpc npc, BaseEntity target)
        {
            if (npc == null || target == null || !_scientists.ContainsKey(npc.net.ID)) return;
            npc.AddTargetGuard(target);
        }

        private void ChangeHomePosition(CustomScientistNpc npc, Vector3 pos)
        {
            if (npc == null || !_scientists.ContainsKey(npc.net.ID)) return;
            npc.HomePosition = pos;
        }
		
		private BasePlayer GetCurrentTarget(CustomScientistNpc npc)
        {
            if (IsCustomScientist(npc)) return npc.CurrentTarget;
            else return null;
        }
        #endregion Methods

        #region Controller
        internal class DefaultSettings { public float EffectiveRange; public float AttackLengthMin; public float AttackLengthMax; }

        private static readonly Dictionary<string, DefaultSettings> _weapons = new Dictionary<string, DefaultSettings>
        {
            ["rifle.bolt"] = new DefaultSettings { EffectiveRange = 150f, AttackLengthMin = -1f, AttackLengthMax = -1f },
            ["bow.compound"] = new DefaultSettings { EffectiveRange = 15f, AttackLengthMin = -1f, AttackLengthMax = -1f },
            ["smg.2"] = new DefaultSettings { EffectiveRange = 20f, AttackLengthMin = 0.4f, AttackLengthMax = 0.4f },
            ["shotgun.double"] = new DefaultSettings { EffectiveRange = 10f, AttackLengthMin = 0.3f, AttackLengthMax = 1f },
            ["pistol.eoka"] = new DefaultSettings { EffectiveRange = 10f, AttackLengthMin = -1f, AttackLengthMax = -1f },
            ["rifle.l96"] = new DefaultSettings { EffectiveRange = 150f, AttackLengthMin = -1f, AttackLengthMax = -1f },
            ["pistol.nailgun"] = new DefaultSettings { EffectiveRange = 15f, AttackLengthMin = 0f, AttackLengthMax = 0.46f },
            ["pistol.python"] = new DefaultSettings { EffectiveRange = 15f, AttackLengthMin = 0.175f, AttackLengthMax = 0.525f },
            ["pistol.semiauto"] = new DefaultSettings { EffectiveRange = 15f, AttackLengthMin = 0f, AttackLengthMax = 0.46f },
            ["smg.thompson"] = new DefaultSettings { EffectiveRange = 20f, AttackLengthMin = 0.4f, AttackLengthMax = 0.4f },
            ["shotgun.waterpipe"] = new DefaultSettings { EffectiveRange = 10f, AttackLengthMin = -1f, AttackLengthMax = -1f },
            ["multiplegrenadelauncher"] = new DefaultSettings { EffectiveRange = 20f, AttackLengthMin = -1f, AttackLengthMax = -1f },
            ["snowballgun"] = new DefaultSettings { EffectiveRange = 5f, AttackLengthMin = 2f, AttackLengthMax = 2f }
        };

        private static readonly HashSet<string> _meleeWeapons = new HashSet<string>
        {
            "bone.club",
            "knife.bone",
            "knife.butcher",
            "candycaneclub",
            "knife.combat",
            "longsword",
            "mace",
            "machete",
            "paddle",
            "pitchfork",
            "salvaged.cleaver",
            "salvaged.sword",
            "spear.stone",
            "spear.wooden",
            "chainsaw",
            "hatchet",
            "jackhammer",
            "pickaxe",
            "axe.salvaged",
            "hammer.salvaged",
            "icepick.salvaged",
            "stonehatchet",
            "stone.pickaxe",
            "torch",
            "sickle"
        };

        private static readonly HashSet<string> _firstDistanceWeapons = new HashSet<string>
        {
            "bow.compound",
            "shotgun.double",
            "pistol.eoka",
            "flamethrower",
            "pistol.m92",
            "pistol.nailgun",
            "multiplegrenadelauncher",
            "shotgun.pump",
            "pistol.python",
            "pistol.revolver",
            "pistol.semiauto",
            "snowballgun",
            "shotgun.spas12",
            "shotgun.waterpipe"
        };

        private static readonly HashSet<string> _secondDistanceWeapons = new HashSet<string>
        {
            "smg.2",
            "smg.mp5",
            "rifle.semiauto",
            "smg.thompson"
        };

        private static readonly HashSet<string> _thirdDistanceWeapons = new HashSet<string>
        {
            "rifle.ak",
            "rifle.lr300",
            "lmg.m249",
            "rifle.m39",
            "hmlmg",
            "rifle.ak.ice"
        };

        private static readonly HashSet<string> _fourthDistanceWeapons = new HashSet<string>
        {
            "rifle.bolt",
            "rifle.l96"
        };

        public class CustomScientistNpc : ScientistNPC
        {
            public NpcConfig Config { get; set; }

            public BasePlayer CurrentTarget { get; set; }

            public AttackEntity CurrentWeapon { get; set; }

            public Vector3 HomePosition { get; set; }

            public float DistanceFromBase => Vector3.Distance(transform.position, HomePosition);

            public float DistanceToTarget => Vector3.Distance(transform.position, CurrentTarget.transform.position);

            public override void ServerInit()
            {
                base.ServerInit();

                if (string.IsNullOrEmpty(Config.HomePosition)) HomePosition = transform.position;
                else HomePosition = Config.HomePosition.ToVector3();

                if (NavAgent == null) NavAgent = GetComponent<NavMeshAgent>();
                if (NavAgent != null)
                {
                    NavAgent.areaMask = Config.AreaMask;
                    NavAgent.agentTypeID = Config.AgentTypeID;
                }

                startHealth = Config.Health;
                _health = Config.Health;

                damageScale = Config.DamageScale;

                if (Config.DisableRadio)
                {
                    CancelInvoke(PlayRadioChatter);
                    RadioChatterEffects = Array.Empty<GameObjectRef>();
                    DeathEffects = Array.Empty<GameObjectRef>();
                }

                ClearContainer(inventory.containerWear);
                ClearContainer(inventory.containerBelt);
                if (!string.IsNullOrEmpty(Config.Kit) && _ins.Kits != null) _ins.Kits.Call("GiveKit", this, Config.Kit);
                else UpdateInventory();

                displayName = Config.Name;

                InvokeRepeating(LightCheck, 1f, 30f);
                InvokeRepeating(UpdateTick, 1f, 2f);
            }

            private void UpdateInventory()
            {
                if (Config.WearItems.Count > 0)
                {
                    foreach (Item item in Config.WearItems.Select(x => ItemManager.CreateByName(x.ShortName, 1, x.SkinID)))
                    {
                        if (item == null) continue;
                        if (!item.MoveToContainer(inventory.containerWear)) item.Remove();
                    }
                }
                if (Config.BeltItems.Count > 0)
                {
                    foreach (NpcBelt npcItem in Config.BeltItems)
                    {
                        Item item = ItemManager.CreateByName(npcItem.ShortName, npcItem.Amount, npcItem.SkinID);
                        if (item == null) continue;
                        foreach (ItemDefinition itemDefinition in npcItem.Mods.Select(ItemManager.FindItemDefinition).Where(x => x != null)) item.contents.AddItem(itemDefinition, 1);
                        if (!item.MoveToContainer(inventory.containerBelt)) item.Remove();
                    }
                }
            }

            private static void ClearContainer(ItemContainer container)
            {
                List<Item> allItems = container.itemList;
                for (int i = allItems.Count - 1; i >= 0; i--)
                {
                    Item item = allItems[i];
                    item.RemoveFromContainer();
                    item.Remove();
                }
            }

            private void OnDestroy()
            {
                if (_healCoroutine != null) ServerMgr.Instance.StopCoroutine(_healCoroutine);
                if (_fireC4Coroutine != null) ServerMgr.Instance.StopCoroutine(_fireC4Coroutine);
                if (_fireRocketLauncherCoroutine != null) ServerMgr.Instance.StopCoroutine(_fireRocketLauncherCoroutine);
                CancelInvoke();
            }

            private void UpdateTick()
            {
                if (CanRunAwayWater()) RunAwayWater();

                if (CanThrownGrenade()) ThrownGrenade(CurrentTarget.transform.position);

                if (CanHeal()) _healCoroutine = ServerMgr.Instance.StartCoroutine(Heal());

                EquipWeapon();

                if (Config.States.Contains("RaidState") && Foundations.Count == 0)
                {
                    if (CurrentTarget == null)
                    {
                        FirstTarget = null;
                        CurrentRaidTarget = null;
                    }
                    else
                    {
                        BuildingBlock block = GetNearEntity<BuildingBlock>(CurrentTarget.transform.position, 0.1f, 1 << 21);
                        if (block.IsExists() && IsTeam(CurrentTarget, block.OwnerID)) FirstTarget = block;
                        else
                        {
                            FirstTarget = null;
                            CurrentRaidTarget = null;
                        }
                    }
                }

                if (_beforeGuardHomePosition != Vector3.zero)
                {
                    if (_guardTarget.IsExists()) HomePosition = _guardTarget.transform.position;
                    else
                    {
                        HomePosition = _beforeGuardHomePosition;
                        _beforeGuardHomePosition = Vector3.zero;
                        _guardTarget = null;
                        Interface.Oxide.CallHook("OnCustomNpcGuardTargetEnd", this);
                    }
                }
            }

            #region Targeting
            public new BasePlayer GetBestTarget()
            {
                if (IsRunAwayWater) return null;
                BasePlayer target = null;
                float delta = -1f;
                foreach (BasePlayer basePlayer in Brain.Senses.Players)
                {
                    if (!CanTargetBasePlayer(basePlayer) || Interface.CallHook("OnCustomNpcTarget", this, basePlayer) != null) continue;
                    float rangeDelta = 1f - Mathf.InverseLerp(1f, Brain.SenseRange, Vector3.Distance(basePlayer.transform.position, transform.position));
                    if (Config.Sensory.CheckVisionCone)
                    {
                        float dot = Vector3.Dot((basePlayer.transform.position - eyes.position).normalized, eyes.BodyForward());
                        if (dot < Brain.VisionCone) continue;
                        rangeDelta += Mathf.InverseLerp(Brain.VisionCone, 1f, dot) / 2f;
                    }
                    rangeDelta += (Brain.Senses.Memory.IsLOS(basePlayer) ? 2f : 0f);
                    if (rangeDelta <= delta) continue;
                    target = basePlayer;
                    delta = rangeDelta;
                }
                return target;
            }

            public bool CanTargetBasePlayer(BasePlayer target)
            {
                if (IsRunAwayWater) return false;
                if (!target.IsPlayer() || target.Health() <= 0f) return false;
                if (target.IsSleeping() || target.IsWounded() || target.IsDead() || target.InSafeZone()) return false;
                if (target._limitedNetworking) return false;
                return true;
            }
            #endregion Targeting

            #region Equip Weapons
            private bool _isEquiping = false;

            private bool CanEquipWeapon()
            {
                if (inventory == null || inventory.containerBelt == null) return false;
                if (_isEquiping) return false;
                if (IsFireRocketLauncher) return false;
                if (_isHealing) return false;
                if (IsMounted() && !Config.CanUseWeaponMounted) return false;
                return true;
            }

            public override void EquipWeapon(bool skipDeployDelay = false)
            {
                if (!CanEquipWeapon()) return;
                Item weapon = null;
                if (CurrentTarget == null)
                {
                    if (CurrentWeapon == null)
                    {
                        Dictionary<int, List<Item>> weapons = new Dictionary<int, List<Item>> { [0] = new List<Item>(), [1] = new List<Item>(), [2] = new List<Item>(), [3] = new List<Item>(), [4] = new List<Item>() };
                        foreach (Item item in inventory.containerBelt.itemList)
                        {
                            int type = GetTypeWeaponItem(item);
                            if (type == -1) continue;
                            weapons[type].Add(item);
                        }
                        if (weapons[3].Count > 0) weapon = weapons[3].GetRandom();
                        else if (weapons[2].Count > 0) weapon = weapons[2].GetRandom();
                        else if (weapons[1].Count > 0) weapon = weapons[1].GetRandom();
                        else if (weapons[4].Count > 0) weapon = weapons[4].GetRandom();
                        else if (weapons[0].Count > 0) weapon = weapons[0].GetRandom();
                    }
                    else return;
                }
                else
                {
                    float distanceToTarget = DistanceToTarget;
                    int type = -1;
                    foreach (Item item in inventory.containerBelt.itemList)
                    {
                        int currentType = GetTypeWeaponItem(item);
                        if (currentType == -1) continue;
                        if (type == -1)
                        {
                            weapon = item;
                            type = currentType;
                        }
                        else
                        {
                            if (type == currentType) continue;
                            float oldDistance = type > 0 ? Config.Sensory.AttackRangeMultiplier * type * 10f : 2f;
                            float newDistance = currentType > 0 ? Config.Sensory.AttackRangeMultiplier * currentType * 10f : 2f;
                            if ((oldDistance > distanceToTarget && newDistance > distanceToTarget && newDistance < oldDistance) ||
                                (oldDistance < distanceToTarget && newDistance > distanceToTarget) ||
                                (oldDistance < distanceToTarget && newDistance < distanceToTarget && newDistance > oldDistance))
                            {
                                weapon = item;
                                type = currentType;
                            }
                        }
                    }
                }
                if (weapon == null) return;
                AttackEntity attackEntity = weapon.GetHeldEntity() as AttackEntity;
                if (attackEntity == null) return;
                if (CurrentWeapon == attackEntity) return;
                _isEquiping = true;
                UpdateActiveItem(weapon.uid);
                CurrentWeapon = attackEntity;
                attackEntity.TopUpAmmo();
                if (attackEntity is Chainsaw) (attackEntity as Chainsaw).ServerNPCStart();
                if (attackEntity is BaseProjectile)
                {
                    if (_weapons.ContainsKey(weapon.info.shortname))
                    {
                        attackEntity.effectiveRange = _weapons[weapon.info.shortname].EffectiveRange;
                        attackEntity.attackLengthMin = _weapons[weapon.info.shortname].AttackLengthMin;
                        attackEntity.attackLengthMax = _weapons[weapon.info.shortname].AttackLengthMax;
                    }
                    string ammo = Config.BeltItems.FirstOrDefault(x => x.ShortName == weapon.info.shortname).Ammo;
                    if (!string.IsNullOrEmpty(ammo))
                    {
                        BaseProjectile baseProjectile = attackEntity as BaseProjectile;
                        baseProjectile.primaryMagazine.ammoType = ItemManager.FindItemDefinition(ammo);
                        baseProjectile.SendNetworkUpdateImmediate();
                    }
                }
                Invoke(FinishEquiping, 1.5f);
            }

            private void FinishEquiping() => _isEquiping = false;

            private int GetTypeWeaponItem(Item item)
            {
                if (_meleeWeapons.Contains(item.info.shortname)) return 0;
                if (_firstDistanceWeapons.Contains(item.info.shortname)) return 1;
                if (_secondDistanceWeapons.Contains(item.info.shortname)) return 2;
                if (_thirdDistanceWeapons.Contains(item.info.shortname)) return 3;
                if (_fourthDistanceWeapons.Contains(item.info.shortname)) return 4;
                return -1;
            }

            internal void HolsterWeapon()
            {
                if (CurrentWeapon == null) return;
                CurrentWeapon.SetHeld(false);
                CurrentWeapon = null;
                SendNetworkUpdate();
                inventory.UpdatedVisibleHolsteredItems();
            }
            #endregion Equip Weapons

            protected override string OverrideCorpseName() => displayName;

            public override float GetAimConeScale() => Config.AimConeScale;

            #region Heal
            private Coroutine _healCoroutine = null;
            private bool _isHealing = false;

            private bool CanHeal()
            {
                if (_isHealing || health >= Config.Health || CurrentTarget != null || IsFireC4 || IsFireRocketLauncher || _isEquiping) return false;
                return inventory.containerBelt.itemList.Any(x => x.info.shortname == "syringe.medical");
            }

            private IEnumerator Heal()
            {
                _isHealing = true;
                Item syringe = inventory.containerBelt.itemList.FirstOrDefault(x => x.info.shortname == "syringe.medical");
                CurrentWeapon = null;
                UpdateActiveItem(syringe.uid);
                MedicalTool medicalTool = syringe.GetHeldEntity() as MedicalTool;
                yield return CoroutineEx.waitForSeconds(1.5f);
                medicalTool.ServerUse();
                InitializeHealth(health + 15f > Config.Health ? Config.Health : health + 15f, Config.Health);
                yield return CoroutineEx.waitForSeconds(2f);
                _isHealing = false;
                EquipWeapon();
            }
            #endregion Heal

            #region Grenades
            private readonly HashSet<string> _barricades = new HashSet<string>
            {
                "barricade.cover.wood",
                "barricade.sandbags",
                "barricade.concrete",
                "barricade.stone"
            };
            private bool _isReloadGrenade = false;
            private bool _isReloadSmoke = false;

            private void FinishReloadGrenade() => _isReloadGrenade = false;

            private void FinishReloadSmoke() => _isReloadSmoke = false;

            private bool CanThrownGrenade()
            {
                if (_isReloadGrenade || CurrentTarget == null) return false;
                if (IsMounted() && !Config.CanUseWeaponMounted) return false;
                return DistanceToTarget < 15f && inventory.containerBelt.itemList.Any(x => x.info.shortname == "grenade.f1" || x.info.shortname == "grenade.beancan" || x.info.shortname == "grenade.molotov" || x.info.shortname == "grenade.flashbang") && (!CanSeeTarget(CurrentTarget) || IsBehindBarricade());
            }

            internal bool IsBehindBarricade() => CanSeeTarget(CurrentTarget) && IsBarricade();

            private bool IsBarricade()
            {
                SetAimDirection((CurrentTarget.transform.position - transform.position).normalized);
                RaycastHit[] hits = Physics.RaycastAll(eyes.HeadRay());
                GamePhysics.Sort(hits);
                return hits.Select(x => x.GetEntity() as Barricade).Any(x => x != null && _barricades.Contains(x.ShortPrefabName) && Vector3.Distance(transform.position, x.transform.position) < DistanceToTarget);
            }

            private void ThrownGrenade(Vector3 target)
            {
                Item item = inventory.containerBelt.itemList.FirstOrDefault(x => x.info.shortname == "grenade.f1" || x.info.shortname == "grenade.beancan" || x.info.shortname == "grenade.molotov" || x.info.shortname == "grenade.flashbang");
                if (item != null)
                {
                    GrenadeWeapon weapon = item.GetHeldEntity() as GrenadeWeapon;
                    if (weapon != null)
                    {
                        Brain.Navigator.Stop();
                        SetAimDirection((target - transform.position).normalized);
                        weapon.ServerThrow(target);
                        _isReloadGrenade = true;
                        Invoke(FinishReloadGrenade, 10f);
                    }
                }
            }

            internal void ThrownSmoke()
            {
                if (!_isReloadSmoke)
                {
                    Item item = inventory.containerBelt.itemList.FirstOrDefault(x => x.info.shortname == "grenade.smoke");
                    if (item != null)
                    {
                        GrenadeWeapon weapon = item.GetHeldEntity() as GrenadeWeapon;
                        if (weapon != null)
                        {
                            weapon.ServerThrow(transform.position);
                            _isReloadSmoke = true;
                            Invoke(FinishReloadSmoke, 30f);
                        }
                    }
                }
            }
            #endregion Grenades

            #region Run Away Water
            internal bool IsRunAwayWater = false;

            private bool CanRunAwayWater()
            {
                if (!Config.CanRunAwayWater || IsRunAwayWater) return false;
                if (CurrentTarget == null)
                {
                    if (transform.position.y < -0.25f) return true;
                    else return false;
                }
                if (transform.position.y > -0.25f || TerrainMeta.HeightMap.GetHeight(CurrentTarget.transform.position) > -0.25f) return false;
                if (CurrentWeapon is BaseProjectile && DistanceToTarget < EngagementRange()) return false;
                if (CurrentWeapon is BaseMelee && DistanceToTarget < CurrentWeapon.effectiveRange) return false;
                return true;
            }

            private void RunAwayWater()
            {
                IsRunAwayWater = true;
                CurrentTarget = null;
                Invoke(FinishRunAwayWater, 20f);
            }

            private void FinishRunAwayWater() => IsRunAwayWater = false;
            #endregion Run Away Water

            #region Raid
            internal bool IsReloadC4 = false;
            internal bool IsReloadRocketLauncher = false;
            internal bool IsFireRocketLauncher = false;
            internal bool IsFireC4 = false;
            private Coroutine _fireC4Coroutine = null;
            private Coroutine _fireRocketLauncherCoroutine = null;
            internal BaseCombatEntity Turret = null;
            internal BaseCombatEntity FirstTarget = null;
            internal BuildingPrivlidge MainCupboard = null;
            internal HashSet<BoxStorage> Boxes = new HashSet<BoxStorage>();
            internal HashSet<BuildingPrivlidge> Cupboards = new HashSet<BuildingPrivlidge>();
            internal HashSet<BuildingBlock> Foundations = new HashSet<BuildingBlock>();
            internal BaseCombatEntity CurrentRaidTarget = null;

            internal void AddTargetRaid(BuildingPrivlidge cupboard)
            {
                Cupboards = GetCupboards(cupboard);
                Boxes = GetBoxes(Cupboards);
                HashSet<BuildingBlock> allBlocks = GetBlocks(Cupboards);
                Foundations = allBlocks.Where(x => x.ShortPrefabName.Contains("foundation"));
                Vector3 centerHome = GetCenterHomePos(allBlocks);
                MainCupboard = Cupboards.Min(x => Vector3.Distance(x.transform.position, centerHome));
                Cupboards.Remove(MainCupboard);
            }

            internal void AddTargetRaidMelee(BuildingPrivlidge cupboard) { Foundations = GetBlocks(GetCupboards(cupboard)).Where(x => x.ShortPrefabName.Contains("foundation")); }

            internal void AddTurret(BaseCombatEntity turret)
            {
                if (!Turret.IsExists() || Vector3.Distance(transform.position, turret.transform.position) < Vector3.Distance(transform.position, Turret.transform.position))
                {
                    Turret = turret;
                    BuildingBlock block = GetNearEntity<BuildingBlock>(Turret.transform.position, 0.1f, 1 << 21);
                    CurrentRaidTarget = block.IsExists() ? block : Turret;
                }
            }

            private static T GetNearEntity<T>(Vector3 position, float radius, int layerMask) where T : BaseCombatEntity
            {
                List<T> list = new List<T>();
                Vis.Entities(position, radius, list, layerMask);
                return list.Count == 0 ? null : list.Min(s => Vector3.Distance(position, s.transform.position));
            }

            private static List<T> GetEntities<T>(Vector3 position, float radius, int layerMask) where T : BaseCombatEntity
            {
                List<T> list = new List<T>();
                Vis.Entities(position, radius, list, layerMask);
                return list.Count == 0 ? null : list;
            }

            private static Vector3 GetCenterHomePos(HashSet<BuildingBlock> blocks)
            {
                float Xmin = blocks.Min(x => x.transform.position.x).transform.position.x;
                float Xmax = blocks.Max(x => x.transform.position.x).transform.position.x;
                float Ymin = blocks.Min(x => x.transform.position.y).transform.position.y;
                float Ymax = blocks.Max(x => x.transform.position.y).transform.position.y;
                float Zmin = blocks.Min(x => x.transform.position.z).transform.position.z;
                float Zmax = blocks.Max(x => x.transform.position.z).transform.position.z;
                return new Vector3((Xmin + Xmax) / 2, (Ymin + Ymax) / 2, (Zmin + Zmax) / 2);
            }

            private static HashSet<BuildingPrivlidge> GetCupboards(BuildingPrivlidge cupboard)
            {
                HashSet<ulong> ids = cupboard.authorizedPlayers.Select(x => x.userid).ToHashSet();
                return GetEntities<BuildingPrivlidge>(cupboard.transform.position, 100f, 1 << 8).Where(x => x.authorizedPlayers.Any(y => ids.Contains(y.userid))).ToHashSet();
            }

            private static HashSet<BuildingBlock> GetBlocks(HashSet<BuildingPrivlidge> cupboards) => cupboards.SelectMany(x => x.GetBuilding().buildingBlocks);

            private static HashSet<BoxStorage> GetBoxes(HashSet<BuildingPrivlidge> cupboards) => cupboards.SelectMany(x => x.GetBuilding().decayEntities.OfType<BoxStorage>());

            internal BaseCombatEntity GetRaidTarget()
            {
                BaseCombatEntity result = GetRaidMainTarget();
                if (result == null) return null;
                BaseCombatEntity targetPath = GetTargetPath(result);
                return targetPath != null ? targetPath : result;
            }

            internal BaseCombatEntity GetRaidTargetMelee()
            {
                if (Foundations.Count == 0) return null;
                foreach (BuildingBlock block in Foundations.Where(x => !x.IsExists())) Foundations.Remove(block);
                if (Foundations.Count == 0) return null;
                BaseCombatEntity result = Foundations.Min(x => Vector3.Distance(transform.position, x.transform.position));
                NavMeshHit navMeshHit;
                if (NavMesh.SamplePosition(result.transform.position, out navMeshHit, 3f, NavAgent.areaMask))
                {
                    NavMeshPath path = new NavMeshPath();
                    if (NavMesh.CalculatePath(transform.position, navMeshHit.position, NavAgent.areaMask, path) && path.status != NavMeshPathStatus.PathComplete) result = GetNearEntity<BaseCombatEntity>(path.corners.Last(), 3f, 1 << 8 | 1 << 21);
                }
                return result;
            }

            private BaseCombatEntity GetRaidMainTarget()
            {
                if (Turret.IsExists())
                {
                    BuildingBlock block = GetNearEntity<BuildingBlock>(Turret.transform.position, 0.1f, 1 << 21);
                    return block.IsExists() ? block : Turret;
                }
                if (FirstTarget.IsExists()) return FirstTarget;
                if (MainCupboard.IsExists()) return MainCupboard;
                if (Boxes.Count > 0)
                {
                    foreach (BoxStorage storage in Boxes.Where(x => !x.IsExists())) Boxes.Remove(storage);
                    if (Boxes.Count > 0) return Boxes.Min(x => Vector3.Distance(transform.position, x.transform.position));
                }
                if (Cupboards.Count > 0)
                {
                    foreach (BuildingPrivlidge cupboard in Cupboards.Where(x => !x.IsExists())) Cupboards.Remove(cupboard);
                    if (Cupboards.Count > 0) return Cupboards.Min(x => Vector3.Distance(transform.position, x.transform.position));
                }
                if (Foundations.Count > 0)
                {
                    foreach (BuildingBlock block in Foundations.Where(x => !x.IsExists())) Foundations.Remove(block);
                    if (Foundations.Count > 0) return Foundations.Min(x => Vector3.Distance(transform.position, x.transform.position));
                }
                return null;
            }

            private BaseCombatEntity GetTargetPath(BaseCombatEntity target)
            {
                NavMeshHit navMeshHit;
                int attempts = 0;
                while (attempts < 20)
                {
                    if (target == null) return null;

                    attempts++;

                    float targetHeight = TerrainMeta.HeightMap.GetHeight(target.transform.position);
                    if (target.transform.position.y - targetHeight > 15f)
                    {
                        List<BuildingBlock> blocks = GetEntities<BuildingBlock>(new Vector3(target.transform.position.x, targetHeight, target.transform.position.z), 30f, 1 << 21);
                        if (blocks != null) target = blocks.Min(s => Vector3.Distance(s.transform.position, transform.position));
                        else return null;
                    }

                    if (NavMesh.SamplePosition(target.transform.position, out navMeshHit, 30f, NavAgent.areaMask))
                    {
                        NavMeshPath path = new NavMeshPath();
                        if (NavMesh.CalculatePath(transform.position, navMeshHit.position, NavAgent.areaMask, path)) return path.status == NavMeshPathStatus.PathComplete ? target : GetNearEntity<BaseCombatEntity>(path.corners.Last(), 5f, 1 << 8 | 1 << 21);
                    }

                    float x1 = UnityEngine.Random.Range(target.transform.position.x - 30f - 5f, target.transform.position.x - 30f);
                    float x2 = UnityEngine.Random.Range(target.transform.position.x + 30f, target.transform.position.x + 30f + 5f);
                    float z1 = UnityEngine.Random.Range(target.transform.position.z - 30f - 5f, target.transform.position.z - 30f);
                    float z2 = UnityEngine.Random.Range(target.transform.position.z + 30f, target.transform.position.z + 30f + 5f);

                    Vector3 vector1 = new Vector3(x1, 500f, z1);
                    vector1.y = TerrainMeta.HeightMap.GetHeight(vector1);
                    Vector3 vector2 = new Vector3(x2, 500f, z1);
                    vector2.y = TerrainMeta.HeightMap.GetHeight(vector2);
                    Vector3 vector3 = new Vector3(x1, 500f, z2);
                    vector3.y = TerrainMeta.HeightMap.GetHeight(vector3);
                    Vector3 vector4 = new Vector3(x2, 500f, z2);
                    vector4.y = TerrainMeta.HeightMap.GetHeight(vector4);
                    HashSet<Vector3> list = new HashSet<Vector3> { vector1, vector2, vector3, vector4 };

                    target = GetNearEntity<BaseCombatEntity>(list.Min(x => Vector3.Distance(transform.position, x)), 5f, 1 << 8 | 1 << 21);
                }
                return null;
            }

            internal bool StartExplosion(BaseCombatEntity target)
            {
                if (target == null) return false;
                if (CanThrownC4(target))
                {
                    _fireC4Coroutine = ServerMgr.Instance.StartCoroutine(ThrownC4(target));
                    return true;
                }
                if (CanRaidRocketLauncher(target))
                {
                    ThrownSmoke();
                    _fireRocketLauncherCoroutine = ServerMgr.Instance.StartCoroutine(ProcessFireRocketLauncher(target));
                    return true;
                }
                return false;
            }

            private bool CanRaidRocketLauncher(BaseCombatEntity target) => !IsReloadRocketLauncher && !IsFireRocketLauncher && inventory.containerBelt.itemList.Any(x => x.info.shortname == "rocket.launcher") && !_isEquiping && !_isHealing && Vector3.Distance(transform.position, target.transform.position) < 30f;

            private IEnumerator ProcessFireRocketLauncher(BaseCombatEntity target)
            {
                IsFireRocketLauncher = true;
                EquipRocketLauncher();
                if (!IsMounted()) SetDucked(true);
                Brain.Navigator.Stop();
                Brain.Navigator.SetFacingDirectionEntity(target);
                yield return CoroutineEx.waitForSeconds(1.5f);
                if (target.IsExists())
                {
                    if (target.ShortPrefabName.Contains("foundation"))
                    {
                        Brain.Navigator.ClearFacingDirectionOverride();
                        SetAimDirection((target.transform.position - new Vector3(0f, 1.5f, 0f) - transform.position).normalized);
                    }
                    FireRocketLauncher();
                    IsReloadRocketLauncher = true;
                    Invoke(FinishReloadRocketLauncher, 6f);
                }
                IsFireRocketLauncher = false;
                EquipWeapon();
                Brain.Navigator.ClearFacingDirectionOverride();
                if (!IsMounted()) SetDucked(false);
            }

            private void EquipRocketLauncher()
            {
                Item item = inventory.containerBelt.itemList.FirstOrDefault(x => x.info.shortname == "rocket.launcher");
                CurrentWeapon = null;
                UpdateActiveItem(item.uid);
            }

            private void FireRocketLauncher()
            {
                RaycastHit raycastHit;
                SignalBroadcast(Signal.Attack, string.Empty);
                Vector3 vector3 = IsMounted() ? eyes.position + new Vector3(0f, 0.5f, 0f) : eyes.position;
                Vector3 modifiedAimConeDirection = AimConeUtil.GetModifiedAimConeDirection(2.25f, eyes.BodyForward());
                float single = 1f;
                if (Physics.Raycast(vector3, modifiedAimConeDirection, out raycastHit, single, 1236478737)) single = raycastHit.distance - 0.1f;
                TimedExplosive rocket = GameManager.server.CreateEntity("assets/prefabs/ammo/rocket/rocket_basic.prefab", vector3 + modifiedAimConeDirection * single) as TimedExplosive;
                rocket.creatorEntity = this;
                ServerProjectile serverProjectile = rocket.GetComponent<ServerProjectile>();
                serverProjectile.InitializeVelocity(GetInheritedProjectileVelocity(modifiedAimConeDirection) + modifiedAimConeDirection * serverProjectile.speed * 2f);
                rocket.Spawn();
            }

            private void FinishReloadRocketLauncher() => IsReloadRocketLauncher = false;

            private bool CanThrownC4(BaseCombatEntity target) => !IsReloadC4 && !IsFireC4 && inventory.containerBelt.itemList.Any(x => x.info.shortname == "explosive.timed") && Vector3.Distance(transform.position, target.transform.position) < 5f;

            private IEnumerator ThrownC4(BaseCombatEntity target)
            {
                Item item = inventory.containerBelt.itemList.FirstOrDefault(x => x.info.shortname == "explosive.timed");
                IsFireC4 = true;
                Brain.Navigator.Stop();
                Brain.Navigator.SetFacingDirectionEntity(target);
                yield return CoroutineEx.waitForSeconds(1.5f);
                if (target.IsExists())
                {
                    (item.GetHeldEntity() as ThrownWeapon).ServerThrow(target.transform.position);
                    IsReloadC4 = true;
                    Invoke(FinishReloadC4, 15f);
                }
                IsFireC4 = false;
                Brain.Navigator.ClearFacingDirectionOverride();
            }

            private void FinishReloadC4() => IsReloadC4 = false;

            private static bool IsTeam(BasePlayer player, ulong targetId)
            {
                if (player == null || targetId == 0) return false;
                if (player.userID == targetId) return true;
                if (player.currentTeam != 0)
                {
                    RelationshipManager.PlayerTeam playerTeam = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                    if (playerTeam == null) return false;
                    if (playerTeam.members.Contains(targetId)) return true;
                }
                if (_ins.plugins.Exists("Friends") && (bool)_ins.Friends.Call("AreFriends", player.userID, targetId)) return true;
                if (_ins.plugins.Exists("Clans") && _ins.Clans.Author == "k1lly0u" && (bool)_ins.Clans.Call("IsMemberOrAlly", player.UserIDString, targetId.ToString())) return true;
                return false;
            }
            #endregion Raid

            #region Guard
            private Vector3 _beforeGuardHomePosition = Vector3.zero;
            private BaseEntity _guardTarget = null;

            internal void AddTargetGuard(BaseEntity target)
            {
                _beforeGuardHomePosition = HomePosition;
                _guardTarget = target;
            }
            #endregion Guard

            #region Multiple Grenade Launcher
            internal bool IsReloadGrenadeLauncher = false;
            private int _countAmmoInGrenadeLauncher = 6;

            internal void FireGrenadeLauncher()
            {
                RaycastHit raycastHit;
                SignalBroadcast(Signal.Attack, string.Empty);
                Vector3 vector3 = IsMounted() ? eyes.position + new Vector3(0f, 0.5f, 0f) : eyes.position;
                Vector3 modifiedAimConeDirection = AimConeUtil.GetModifiedAimConeDirection(0.675f, eyes.BodyForward());
                float single = 1f;
                if (Physics.Raycast(vector3, modifiedAimConeDirection, out raycastHit, single, 1236478737)) single = raycastHit.distance - 0.1f;
                TimedExplosive grenade = GameManager.server.CreateEntity("assets/prefabs/ammo/40mmgrenade/40mm_grenade_he.prefab", vector3 + modifiedAimConeDirection * single) as TimedExplosive;
                grenade.creatorEntity = this;
                ServerProjectile serverProjectile = grenade.GetComponent<ServerProjectile>();
                serverProjectile.InitializeVelocity(GetInheritedProjectileVelocity(modifiedAimConeDirection) + modifiedAimConeDirection * serverProjectile.speed * 2f);
                grenade.Spawn();
                _countAmmoInGrenadeLauncher--;
                if (_countAmmoInGrenadeLauncher == 0)
                {
                    IsReloadGrenadeLauncher = true;
                    Invoke(FinishReloadGrenadeLauncher, 8f);
                }
            }

            private void FinishReloadGrenadeLauncher()
            {
                _countAmmoInGrenadeLauncher = 6;
                IsReloadGrenadeLauncher = false;
            }
            #endregion Multiple Grenade Launcher

            #region Flame Thrower
            internal bool IsReloadFlameThrower = false;

            internal void FireFlameThrower()
            {
                FlameThrower flameThrower = CurrentWeapon as FlameThrower;
                if (flameThrower == null || flameThrower.IsFlameOn()) return;
                if (flameThrower.ammo <= 0)
                {
                    IsReloadFlameThrower = true;
                    Invoke(FinishReloadFlameThrower, 4f);
                    return;
                }
                flameThrower.SetFlameState(true);
                Invoke(flameThrower.StopFlameState, 0.25f);
            }

            private void FinishReloadFlameThrower()
            {
                FlameThrower flameThrower = CurrentWeapon as FlameThrower;
                if (flameThrower == null) return;
                flameThrower.TopUpAmmo();
                IsReloadFlameThrower = false;
            }
            #endregion Flame Thrower

            #region Melee Weapon
            internal void UseMeleeWeapon(bool damage = true)
            {
                BaseMelee weapon = CurrentWeapon as BaseMelee;
                if (weapon.HasAttackCooldown()) return;
                weapon.StartAttackCooldown(weapon.repeatDelay * 2f);
                SignalBroadcast(Signal.Attack, string.Empty, null);
                if (weapon.swingEffect.isValid) Effect.server.Run(weapon.swingEffect.resourcePath, weapon.transform.position, Vector3.forward, net.connection, false);
                if (!damage) return;
                Vector3 vector31 = eyes.BodyForward();
                for (int i = 0; i < 2; i++)
                {
                    List<RaycastHit> list = Pool.GetList<RaycastHit>();
                    GamePhysics.TraceAll(new Ray(eyes.position - (vector31 * (i == 0 ? 0f : 0.2f)), vector31), (i == 0 ? 0f : weapon.attackRadius), list, weapon.effectiveRange + 0.2f, 1219701521, QueryTriggerInteraction.UseGlobal, null);
                    bool flag = false;
                    for (int j = 0; j < list.Count; j++)
                    {
                        RaycastHit item = list[j];
                        BaseEntity entity = item.GetEntity();
                        if (entity != null && (entity == null || entity != this && !entity.EqualNetID(this)) && (entity == null || !entity.isClient) && entity.Categorize() == "player")
                        {
                            float single = weapon.damageTypes.Sum(x => x.amount);
                            entity.OnAttacked(new HitInfo(this, entity, DamageType.Slash, single * weapon.npcDamageScale * Config.DamageScale));
                            HitInfo hitInfo = Pool.Get<HitInfo>();
                            hitInfo.HitEntity = entity;
                            hitInfo.HitPositionWorld = item.point;
                            hitInfo.HitNormalWorld = -vector31;
                            if (entity is BaseNpc || entity is BasePlayer) hitInfo.HitMaterial = StringPool.Get("Flesh");
                            else hitInfo.HitMaterial = StringPool.Get(item.GetCollider().sharedMaterial != null ? item.GetCollider().sharedMaterial.GetName() : "generic");
                            weapon.ServerUse_OnHit(hitInfo);
                            Effect.server.ImpactEffect(hitInfo);
                            Pool.Free(ref hitInfo);
                            flag = true;
                            if (entity == null || entity.ShouldBlockProjectiles()) break;
                        }
                    }
                    Pool.FreeList(ref list);
                    if (flag) break;
                }
            }
            #endregion Melee Weapon
        }

        public class CustomScientistBrain : ScientistBrain
        {
            private CustomScientistNpc _npc = null;

            public override void InitializeAI()
            {
                if (_npc == null) _npc = GetBaseEntity() as CustomScientistNpc;

                SenseTypes = EntityType.Player;
                MaxGroupSize = int.MaxValue;
                AttackRangeMultiplier = _npc.Config.Sensory.AttackRangeMultiplier;
                SenseRange = _npc.Config.Sensory.SenseRange;
                TargetLostRange = SenseRange * 2f;
                MemoryDuration = _npc.Config.Sensory.MemoryDuration;
                CheckVisionCone = _npc.Config.Sensory.CheckVisionCone;
                VisionCone = Vector3.Dot(Vector3.forward, Quaternion.Euler(0f, _npc.Config.Sensory.VisionCone, 0f) * Vector3.forward);

                base.InitializeAI();
                UseAIDesign = false;
                ThinkMode = AIThinkMode.Interval;
                thinkRate = 0.25f;
                PathFinder = new HumanPathFinder();
                ((HumanPathFinder)base.PathFinder).Init(_npc);

                Navigator.Speed = _npc.Config.Speed;
            }

            public override void AddStates()
            {
                if (_npc == null) _npc = GetBaseEntity() as CustomScientistNpc;
                states = new Dictionary<AIState, BasicAIState>();
                if (_npc.Config.States.Contains("RoamState")) AddState(new RoamState(_npc));
                if (_npc.Config.States.Contains("ChaseState")) AddState(new ChaseState(_npc));
                if (_npc.Config.States.Contains("CombatState")) AddState(new CombatState(_npc));
                if (_npc.Config.States.Contains("MountedState")) AddState(new MountedState(_npc));
                if (_npc.Config.States.Contains("IdleState")) AddState(new IdleState(_npc));
                if (_npc.Config.States.Contains("CombatStationaryState")) AddState(new CombatStationaryState(_npc));
                if (_npc.Config.States.Contains("RaidState")) AddState(new RaidState(_npc));
                if (_npc.Config.States.Contains("SledgeState")) AddState(new SledgeState(_npc));
                if (_npc.Config.States.Contains("BlazerState")) AddState(new BlazerState(_npc));
            }

            public override void Think(float delta)
            {
                if (_npc == null) return;
                Senses.Update();
                base.Think(delta);
                if (sleeping) return;
                if (!_npc.IsRunAwayWater) _npc.CurrentTarget = _npc.GetBestTarget();
            }

            public new class RoamState : BasicAIState
            {
                private readonly CustomScientistNpc _npc;

                public RoamState(CustomScientistNpc npc) : base(AIState.Roam) { _npc = npc; }

                public override float GetWeight() => 25f;

                public override void StateEnter(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateEnter(brain, entity);
                    _npc.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, true);
                }

                public override void StateLeave(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateLeave(brain, entity);
                    _npc.ThrownSmoke();
                    _npc.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, false);
                }

                public override StateStatus StateThink(float delta, BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateThink(delta, brain, entity);
                    if (_npc.DistanceFromBase > _npc.Config.RoamRange) brain.Navigator.SetDestination(_npc.HomePosition.UpPos(), BaseNavigator.NavigationSpeed.Fast);
                    else if (!brain.Navigator.Moving) brain.Navigator.SetDestination(GetRoamPosition().UpPos(), BaseNavigator.NavigationSpeed.Slowest);
                    return StateStatus.Running;
                }

                private Vector3 GetRoamPosition()
                {
                    Vector3 result = brain.PathFinder.GetRandomPositionAround(_npc.HomePosition, 0f, _npc.Config.RoamRange - 2f < 0f ? 0f : _npc.Config.RoamRange - 2f);
                    NavMeshHit navMeshHit;
                    if (NavMesh.SamplePosition(result, out navMeshHit, 2f, _npc.NavAgent.areaMask))
                    {
                        NavMeshPath path = new NavMeshPath();
                        if (NavMesh.CalculatePath(_npc.transform.position, navMeshHit.position, _npc.NavAgent.areaMask, path)) result = path.status == NavMeshPathStatus.PathComplete ? navMeshHit.position : path.corners.Last();
                        else result = _npc.HomePosition;
                    }
                    else result = _npc.HomePosition;
                    return result;
                }
            }

            public new class ChaseState : BasicAIState
            {
                private readonly CustomScientistNpc _npc;
                private Vector3 _lastTargetPosition = Vector3.zero;

                public ChaseState(CustomScientistNpc npc) : base(AIState.Chase) { _npc = npc; }

                public override float GetWeight()
                {
                    if (_npc.CurrentTarget == null) return 0f;
                    if (_npc.DistanceFromBase > _npc.Config.ChaseRange) return 0f;
                    if (_npc.IsRunAwayWater) return 0f;
                    if (_npc.IsMounted()) return 0f;
                    if (_npc.IsFireC4 || _npc.IsFireRocketLauncher) return 0f;
                    if (_npc.CurrentRaidTarget != null) return 0f;
                    if (!_npc.CanTargetBasePlayer(_npc.CurrentTarget)) return 0f;
                    return 50f;
                }

                public override StateStatus StateThink(float delta, BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateThink(delta, brain, entity);
                    if (_npc.CurrentTarget == null) return StateStatus.Error;
                    if (Vector3.Distance(_lastTargetPosition, _npc.CurrentTarget.transform.position) > 2f)
                    {
                        _lastTargetPosition = _npc.CurrentTarget.transform.position;
                        if (_npc.CurrentWeapon is BaseProjectile) brain.Navigator.SetDestination(GetChasePosition().UpPos(), _npc.DistanceToTarget >= 10f ? BaseNavigator.NavigationSpeed.Fast : BaseNavigator.NavigationSpeed.Normal);
                        else brain.Navigator.SetDestination(GetChasePosition().UpPos(), BaseNavigator.NavigationSpeed.Fast);
                    }
                    return StateStatus.Running;
                }

                private Vector3 GetChasePosition()
                {
                    NavMeshHit navMeshHit;
                    float range = _npc.EngagementRange();
                    int maxDistance = range > 2f ? (int)range : 2;
                    for (int i = 0; i < maxDistance; i++)
                    {
                        if (NavMesh.SamplePosition(_npc.CurrentTarget.transform.position, out navMeshHit, i, _npc.NavAgent.areaMask))
                        {
                            NavMeshPath path = new NavMeshPath();
                            if (NavMesh.CalculatePath(_npc.transform.position, navMeshHit.position, _npc.NavAgent.areaMask, path))
                            {
                                if (path.status == NavMeshPathStatus.PathComplete) return navMeshHit.position;
                                else return path.corners.Last();
                            }
                        }
                    }
                    return _npc.CurrentTarget.transform.position;
                }
            }

            public new class CombatState : BasicAIState
            {
                private readonly CustomScientistNpc _npc;
                private float _nextStrafeTime;

                public CombatState(CustomScientistNpc npc) : base(AIState.Combat) { _npc = npc; }

                public override float GetWeight()
                {
                    if (_npc.CurrentTarget == null || _npc.CurrentWeapon == null) return 0f;
                    if (_npc.DistanceFromBase > _npc.Config.ChaseRange) return 0f;
                    if (_npc.IsRunAwayWater) return 0f;
                    if (_npc.IsMounted()) return 0f;
                    if (_npc.IsFireC4 || _npc.IsFireRocketLauncher) return 0f;
                    if (!_npc.CanTargetBasePlayer(_npc.CurrentTarget)) return 0f;
                    if (_npc.DistanceToTarget > _npc.EngagementRange()) return 0f;
                    if (!_npc.CanSeeTarget(_npc.CurrentTarget) || (_npc.CanSeeTarget(_npc.CurrentTarget) && _npc.IsBehindBarricade())) return 0f;
                    if (_npc.CurrentWeapon.ShortPrefabName == "mgl.entity" && _npc.IsReloadGrenadeLauncher) return 0f;
                    if (_npc.CurrentWeapon is FlameThrower && _npc.IsReloadFlameThrower) return 0f;
                    return 75f;
                }

                public override void StateEnter(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateEnter(brain, entity);
                    brain.mainInterestPoint = _npc.transform.position;
                    brain.Navigator.SetCurrentSpeed(BaseNavigator.NavigationSpeed.Normal);
                    brain.Navigator.SetFacingDirectionEntity(_npc.CurrentTarget);
                }

                public override void StateLeave(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateLeave(brain, entity);
                    _npc.SetDucked(false);
                    brain.Navigator.ClearFacingDirectionOverride();
                }

                public override StateStatus StateThink(float delta, BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateThink(delta, brain, entity);
                    if (_npc.CurrentTarget == null) return StateStatus.Error;
                    brain.Navigator.SetFacingDirectionEntity(_npc.CurrentTarget);
                    if (_npc.CurrentWeapon is BaseProjectile)
                    {
                        if (Time.time > _nextStrafeTime)
                        {
                            if (UnityEngine.Random.Range(0, 3) == 1)
                            {
                                float deltaTime = _npc.CurrentWeapon is BaseLauncher ? UnityEngine.Random.Range(0.5f, 1f) : UnityEngine.Random.Range(1f, 2f);
                                _nextStrafeTime = Time.time + deltaTime;
                                _npc.SetDucked(true);
                                brain.Navigator.Stop();
                            }
                            else
                            {
                                float deltaTime = _npc.CurrentWeapon is BaseLauncher ? UnityEngine.Random.Range(1f, 1.5f) : UnityEngine.Random.Range(2f, 3f);
                                _nextStrafeTime = Time.time + deltaTime;
                                _npc.SetDucked(false);
                                brain.Navigator.SetDestination(brain.PathFinder.GetRandomPositionAround(brain.mainInterestPoint, 1f, 2f).UpPos(), BaseNavigator.NavigationSpeed.Normal);
                            }
                            if (_npc.CurrentWeapon is BaseLauncher) _npc.FireGrenadeLauncher();
                            else _npc.ShotTest(_npc.DistanceToTarget);
                        }
                    }
                    else if (_npc.CurrentWeapon is FlameThrower)
                    {
                        if (_npc.DistanceToTarget < _npc.CurrentWeapon.effectiveRange) _npc.FireFlameThrower();
                        else brain.Navigator.SetDestination(_npc.CurrentTarget.transform.position.UpPos(), BaseNavigator.NavigationSpeed.Fast);
                    }
                    else if (_npc.CurrentWeapon is BaseMelee)
                    {
                        if (_npc.DistanceToTarget < _npc.CurrentWeapon.effectiveRange * 2f) _npc.UseMeleeWeapon();
                        brain.Navigator.SetDestination(_npc.CurrentTarget.transform.position.UpPos(), BaseNavigator.NavigationSpeed.Fast);
                    }
                    return StateStatus.Running;
                }
            }

            public new class MountedState : BasicAIState
            {
                private readonly CustomScientistNpc _npc;

                public MountedState(CustomScientistNpc npc) : base(AIState.Mounted) {_npc = npc; }

                public override float GetWeight() => _npc.IsMounted() ? 100f : 0f;

                public override void StateEnter(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateEnter(brain, entity);
                    if (!_npc.Config.CanUseWeaponMounted) _npc.HolsterWeapon();
                    DisableNavAgent();
                }

                public override void StateLeave(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateLeave(brain, entity);
                    EnableNavAgent();
                    _npc.EquipWeapon();
                }

                private void EnableNavAgent()
                {
                    Vector3 position = _npc.transform.position;
                    _npc.NavAgent.Warp(position);
                    _npc.transform.position = position;
                    _npc.HomePosition = position;
                    _npc.NavAgent.enabled = true;
                    _npc.NavAgent.isStopped = false;
                    brain.Navigator.SetDestination(position, BaseNavigator.NavigationSpeed.Fast);
                }

                private void DisableNavAgent()
                {
                    if (!_npc.NavAgent.enabled) return;
                    _npc.NavAgent.destination = _npc.transform.position;
                    _npc.NavAgent.isStopped = true;
                    _npc.NavAgent.enabled = false;
                }
            }

            public new class IdleState : BasicAIState
            {
                private readonly CustomScientistNpc _npc;

                public IdleState(CustomScientistNpc npc) : base(AIState.Idle) { _npc = npc; }

                public override float GetWeight() => 50f;

                public override void StateEnter(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateEnter(brain, entity);
                    _npc.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, true);
                }

                public override void StateLeave(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateLeave(brain, entity);
                    _npc.ThrownSmoke();
                    _npc.SetPlayerFlag(BasePlayer.PlayerFlags.Relaxed, false);
                }
            }

            public new class CombatStationaryState : BasicAIState
            {
                private readonly CustomScientistNpc _npc;
                private float _nextStrafeTime;

                public CombatStationaryState(CustomScientistNpc npc) : base(AIState.CombatStationary) { _npc = npc; }

                public override float GetWeight()
                {
                    if (_npc.CurrentTarget == null || _npc.CurrentWeapon == null) return 0f;
                    if (_npc.IsFireC4 || _npc.IsFireRocketLauncher) return 0f;
                    if (!_npc.CanTargetBasePlayer(_npc.CurrentTarget)) return 0f;
                    if (_npc.DistanceToTarget > _npc.EngagementRange()) return 0f;
                    if (!_npc.CanSeeTarget(_npc.CurrentTarget)) return 0f;
                    if (_npc.IsBehindBarricade()) return 0f;
                    if (_npc.CurrentWeapon.ShortPrefabName == "mgl.entity" && _npc.IsReloadGrenadeLauncher) return 0f;
                    if (_npc.CurrentWeapon is FlameThrower && _npc.IsReloadFlameThrower) return 0f;
                    return 100f;
                }

                public override void StateEnter(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateEnter(brain, entity);
                    brain.Navigator.SetFacingDirectionEntity(_npc.CurrentTarget);
                }

                public override void StateLeave(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateLeave(brain, entity);
                    if (!_npc.IsMounted()) _npc.SetDucked(false);
                    brain.Navigator.ClearFacingDirectionOverride();
                }

                public override StateStatus StateThink(float delta, BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateThink(delta, brain, entity);
                    if (_npc.CurrentTarget == null) return StateStatus.Error;
                    brain.Navigator.SetFacingDirectionEntity(_npc.CurrentTarget);
                    if (_npc.CurrentWeapon is BaseProjectile)
                    {
                        if (Time.time > _nextStrafeTime)
                        {
                            if (UnityEngine.Random.Range(0, 3) == 1)
                            {
                                float deltaTime = _npc.CurrentWeapon is BaseLauncher ? UnityEngine.Random.Range(0.5f, 1f) : UnityEngine.Random.Range(1f, 2f);
                                _nextStrafeTime = Time.time + deltaTime;
                                if (!_npc.IsMounted()) _npc.SetDucked(true);
                            }
                            else
                            {
                                float deltaTime = _npc.CurrentWeapon is BaseLauncher ? UnityEngine.Random.Range(1f, 1.5f) : UnityEngine.Random.Range(2f, 3f);
                                _nextStrafeTime = Time.time + deltaTime;
                                if (!_npc.IsMounted()) _npc.SetDucked(false);
                            }
                            if (_npc.CurrentWeapon is BaseLauncher) _npc.FireGrenadeLauncher();
                            else _npc.ShotTest(_npc.DistanceToTarget);
                        }
                    }
                    else if (_npc.CurrentWeapon is FlameThrower && _npc.DistanceToTarget < _npc.CurrentWeapon.effectiveRange) _npc.FireFlameThrower();
                    return StateStatus.Running;
                }
            }

            public class RaidState : BasicAIState
            {
                private readonly CustomScientistNpc _npc;

                public RaidState(CustomScientistNpc npc) : base(AIState.Cooldown) { _npc = npc; }

                public override float GetWeight()
                {
                    if (_npc.IsFireC4 || _npc.IsFireRocketLauncher) return 125f;
                    if (_npc.IsRunAwayWater) return 0f;
                    if (_npc.CanTargetBasePlayer(_npc.CurrentTarget) && _npc.CanSeeTarget(_npc.CurrentTarget) && _npc.DistanceToTarget <= _npc.EngagementRange()) return 0f;
                    if (_npc.GetRaidTarget() == null) return 0f;
                    if (_npc.inventory.containerBelt.itemList.Any(x => x.info.shortname == "rocket.launcher") || _npc.inventory.containerBelt.itemList.Any(x => x.info.shortname == "explosive.timed")) return 125f;
                    return 0f;
                }

                public override void StateEnter(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateEnter(brain, entity);
                    _npc.CurrentRaidTarget = _npc.GetRaidTarget();
                }

                public override void StateLeave(BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateLeave(brain, entity);
                    if (!_npc.IsMounted()) _npc.SetDucked(false);
                    brain.Navigator.ClearFacingDirectionOverride();
                }

                public override StateStatus StateThink(float delta, BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateThink(delta, brain, entity);
                    if (_npc.IsFireC4 || _npc.IsFireRocketLauncher) return StateStatus.Running;
                    if (!_npc.CurrentRaidTarget.IsExists())
                    {
                        _npc.CurrentRaidTarget = _npc.GetRaidTarget();
                        if (!_npc.CurrentRaidTarget.IsExists()) return StateStatus.Error;
                    }
                    if (!_npc.StartExplosion(_npc.CurrentRaidTarget) && Vector3.Distance(_npc.transform.position, _npc.CurrentRaidTarget.transform.position) > 5f && !_npc.IsMounted())
                    {
                        _npc.SetDucked(false);
                        brain.Navigator.SetDestination(GetRaidPosition().UpPos(), _npc.CurrentRaidTarget is AutoTurret || _npc.CurrentRaidTarget is GunTrap || _npc.CurrentRaidTarget is FlameTurret || Vector3.Distance(_npc.transform.position, _npc.CurrentRaidTarget.transform.position) > 30f ? BaseNavigator.NavigationSpeed.Fast : Vector3.Distance(_npc.transform.position, _npc.CurrentRaidTarget.transform.position) > 5f ? BaseNavigator.NavigationSpeed.Normal : BaseNavigator.NavigationSpeed.Slow);
                    }
                    return StateStatus.Running;
                }

                private Vector3 GetRaidPosition()
                {
                    if (_npc.CurrentRaidTarget is BuildingPrivlidge || _npc.CurrentRaidTarget is BuildingBlock || _npc.CurrentRaidTarget is BoxStorage) return brain.PathFinder.GetRandomPositionAround(_npc.CurrentRaidTarget.transform.position, 1f, 2f);
                    else
                    {
                        NavMeshHit navMeshHit;
                        if (NavMesh.SamplePosition(_npc.CurrentRaidTarget.transform.position, out navMeshHit, 5f, _npc.NavAgent.areaMask))
                        {
                            NavMeshPath path = new NavMeshPath();
                            if (NavMesh.CalculatePath(_npc.transform.position, navMeshHit.position, _npc.NavAgent.areaMask, path)) return path.status == NavMeshPathStatus.PathComplete ? navMeshHit.position : path.corners.Last();
                        }
                    }
                    return _npc.CurrentRaidTarget.transform.position;
                }
            }

            public class SledgeState : BasicAIState
            {
                private readonly CustomScientistNpc _npc;
                private HashSet<Vector3> _positions = new HashSet<Vector3>();

                public SledgeState(CustomScientistNpc npc) : base(AIState.Cooldown)
                {
                    _npc = npc;
                    _positions = _ins.WallFrames.ToHashSet();
                    _positions.Add(_ins.GeneralPosition);
                }

                public override float GetWeight()
                {
                    if (_npc.CanTargetBasePlayer(_npc.CurrentTarget) && _npc.CanSeeTarget(_npc.CurrentTarget) && IsPath(_npc.CurrentTarget.transform.position)) return 0f;
                    return 125f;
                }

                public override StateStatus StateThink(float delta, BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateThink(delta, brain, entity);

                    Vector3 barricadePos = GetNearBarricadePos;
                    bool haveBarricade = barricadePos != Vector3.zero;

                    Vector3 generalPos = _ins.GeneralPosition;
                    bool haveGeneral = _ins.GeneralPosition != Vector3.zero;

                    bool nearBarricade = haveBarricade && DistanceToPos(barricadePos) < 1.5f;
                    bool nearGeneral = haveGeneral && DistanceToPos(generalPos) < 1.5f;

                    if (nearBarricade || nearGeneral)
                    {
                        _npc.viewAngles = nearBarricade ? Quaternion.LookRotation(barricadePos + new Vector3(0f, 0.5f, 0f) - _npc.transform.position).eulerAngles : Quaternion.LookRotation(generalPos - _npc.transform.position).eulerAngles;
                        if (_npc.CurrentWeapon is BaseMelee) _npc.UseMeleeWeapon(false);
                    }
                    else if (!brain.Navigator.Moving) brain.Navigator.SetDestination(GetResultPos().UpPos(), BaseNavigator.NavigationSpeed.Fast);

                    return StateStatus.Running;
                }

                private bool IsPath(Vector3 pos)
                {
                    NavMeshHit navMeshHit;
                    if (NavMesh.SamplePosition(pos, out navMeshHit, _npc.CurrentWeapon.effectiveRange * 2f, _npc.NavAgent.areaMask))
                    {
                        NavMeshPath path = new NavMeshPath();
                        if (NavMesh.CalculatePath(_npc.transform.position, navMeshHit.position, _npc.NavAgent.areaMask, path))
                        {
                            if (path.status == NavMeshPathStatus.PathComplete) return true;
                            else return DistanceToPos(path.corners.Last()) < _npc.CurrentWeapon.effectiveRange * 2f;
                        }
                        else return false;
                    }
                    else return false;
                }

                private Vector3 GetResultPos()
                {
                    List<Vector3> list = Pool.GetList<Vector3>();
                    list = _positions.Where(x => NecessaryPos(x)).OrderBy(x => DistanceToPos(x));

                    Vector3 point1 = list[0];
                    Vector3 point2 = list[1];

                    float distance0 = DistanceToGeneral;
                    float distance1 = DistanceToPos(point1);
                    float distance2 = DistanceToPos(point2);
                    float distance3 = Vector3.Distance(_ins.GeneralPosition, point1);
                    float distance4 = Vector3.Distance(_ins.GeneralPosition, point2);

                    Pool.FreeList(ref list);

                    Vector3 result = Vector3.zero;

                    if (distance3 < distance4) result = point1;
                    else
                    {
                        if (distance0 >= distance2)
                        {
                            if (distance0 < distance3) result = point2;
                            else result = point1;
                        }
                        else result = point2;
                    }

                    result = brain.PathFinder.GetRandomPositionAround(result, 0f, 1.5f);

                    NavMeshHit navMeshHit;
                    if (NavMesh.SamplePosition(result, out navMeshHit, 1.5f, _npc.NavAgent.areaMask))
                    {
                        NavMeshPath path = new NavMeshPath();
                        if (NavMesh.CalculatePath(_npc.transform.position, navMeshHit.position, _npc.NavAgent.areaMask, path)) result = path.status == NavMeshPathStatus.PathComplete ? navMeshHit.position : path.corners.Last();
                    }

                    return result;
                }

                private float DistanceToGeneral => Vector3.Distance(_npc.transform.position, _ins.GeneralPosition);

                private float DistanceToPos(Vector3 pos) => Vector3.Distance(_npc.transform.position, pos);

                private Vector3 GetNearBarricadePos => _ins.CustomBarricades.Count == 0 ? Vector3.zero : _ins.CustomBarricades.Min(x => DistanceToPos(x));

                private static bool IsEqualVector3(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < 0.1f;

                private bool IsCustomBarricadePos(Vector3 pos) => _ins.CustomBarricades.Any(x => IsEqualVector3(pos, x));

                private bool NecessaryPos(Vector3 pos) => IsEqualVector3(pos, _ins.GeneralPosition) || Vector3.Distance(_npc.transform.position, pos) > 0.5f || IsCustomBarricadePos(pos);
            }

            public class BlazerState : BasicAIState
            {
                private readonly CustomScientistNpc _npc;
                private float _radius;
                private Vector3 _center;
                private List<Vector3> circlePositions = new List<Vector3>();

                public BlazerState(CustomScientistNpc npc) : base(AIState.Cooldown)
                {
                    _npc = npc;
                    _radius = _npc.Config.Sensory.VisionCone;
                    _center = _ins.GeneralPosition;
                    for (int i = 1; i <= 36; i++) circlePositions.Add(new Vector3(_center.x + _radius * Mathf.Sin(i * 10f * Mathf.Deg2Rad), _center.y, _center.z + _radius * Mathf.Cos(i * 10f * Mathf.Deg2Rad)));
                }

                public override float GetWeight()
                {
                    if (IsInside) return 87.5f;
                    if (_npc.CurrentTarget == null) return 87.5f;
                    else
                    {
                        if (IsOutsideTarget) return 0f;
                        else
                        {
                            Vector3 vector3 = GetCirclePos(GetMovePos(_npc.CurrentTarget.transform.position));
                            if (DistanceToPos(vector3) > 2f) return 87.5f;
                            else return 0f;
                        }
                    }
                }

                public override StateStatus StateThink(float delta, BaseAIBrain brain, BaseEntity entity)
                {
                    base.StateThink(delta, brain, entity);
                    if (IsInside) brain.Navigator.SetDestination(GetCirclePos(GetMovePos(_npc.transform.position)).UpPos(), BaseNavigator.NavigationSpeed.Fast);
                    if (_npc.CurrentTarget == null) _npc.CurrentTarget = GetTargetPlayer();
                    if (_npc.CurrentTarget == null) brain.Navigator.SetDestination(GetCirclePos(GetMovePos(_npc.transform.position)).UpPos(), BaseNavigator.NavigationSpeed.Fast);
                    else brain.Navigator.SetDestination(GetNextPos(GetMovePos(_npc.CurrentTarget.transform.position)).UpPos(), BaseNavigator.NavigationSpeed.Fast);
                    return StateStatus.Running;
                }

                private Vector3 GetNextPos(Vector3 targetPos)
                {
                    int numberTarget = circlePositions.IndexOf(GetCirclePos(targetPos));
                    int numberNear = circlePositions.IndexOf(GetNearCirclePos);

                    int countNext = numberTarget < numberNear ? circlePositions.Count - 1 - numberNear + numberTarget : numberTarget - numberNear;

                    if (countNext < 18)
                    {
                        if (numberNear + 1 > 35) return circlePositions[0];
                        else return circlePositions[numberNear + 1];
                    }
                    else
                    {
                        if (numberNear - 1 < 0) return circlePositions[35];
                        else return circlePositions[numberNear - 1];
                    }
                }

                private Vector3 GetCirclePos(Vector3 targetPos) => circlePositions.Min(x => Vector3.Distance(targetPos, x));

                private Vector3 GetMovePos(Vector3 targetPos)
                {
                    Vector3 normal3 = (targetPos - _center).normalized;
                    Vector2 vector2 = new Vector2(normal3.x, normal3.z) * _radius;
                    return _center + new Vector3(vector2.x, _center.y, vector2.y);
                }

                private BasePlayer GetTargetPlayer()
                {
                    List<BasePlayer> list = Pool.GetList<BasePlayer>();
                    Vis.Entities(_center, _npc.Config.ChaseRange, list, 1 << 17);
                    HashSet<BasePlayer> players = list.Where(x => x.IsPlayer());
                    Pool.Free(ref list);
                    return players.Count == 0 ? null : players.Min(x => DistanceToPos(x.transform.position));
                }

                private Vector3 GetNearCirclePos => circlePositions.Min(x => DistanceToPos(x));

                private bool IsInside => DistanceToPos(_center) < _radius - 2f;

                private bool IsOutsideTarget => Vector3.Distance(_center, _npc.CurrentTarget.transform.position) > _radius + 2f;

                private float DistanceToPos(Vector3 pos) => Vector3.Distance(_npc.transform.position, pos);
            }
        }
        #endregion Controller

        #region Oxide Hooks
        [PluginReference] private readonly Plugin Kits, Friends, Clans;

        private static NpcSpawn _ins;

        private readonly Dictionary<uint, CustomScientistNpc> _scientists = new Dictionary<uint, CustomScientistNpc>();

        private void Init() => _ins = this;

        private bool IsCustomScientist(BaseEntity entity) => entity != null && entity.skinID == 11162132011012;

        private void OnServerInitialized()
        {
            CheckVersionPlugin();
            GenerateSpawnpoints();
        }

        private void Unload()
        {
            foreach (CustomScientistNpc npc in _scientists.Values.Where(x => x.IsExists())) npc.Kill();
            _ins = null;
        }

        private void OnEntityKill(CustomScientistNpc npc) { if (npc.IsExists() && _scientists.ContainsKey(npc.net.ID)) _scientists.Remove(npc.net.ID); }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            BaseEntity attacker = info.Initiator;
            if (IsCustomScientist(entity))
            {
                if (attacker == null) return true;
                CustomScientistNpc victimNpc = _scientists[entity.net.ID];
                if (attacker is AutoTurret || attacker is GunTrap || attacker is FlameTurret)
                {
                    if (attacker.OwnerID.IsSteamId()) victimNpc.AddTurret(attacker as BaseCombatEntity);
                    info.damageTypes.ScaleAll(victimNpc.Config.TurretDamageScale);
                    return null;
                }
                BasePlayer attackerBP = attacker as BasePlayer;
                if (attackerBP.IsPlayer())
                {
                    if (victimNpc.CurrentTarget == null && victimNpc.CanTargetBasePlayer(attackerBP)) victimNpc.CurrentTarget = attackerBP;
                    return null;
                }
                return true;
            }
            if (IsCustomScientist(attacker))
            {
                if ((entity as BasePlayer).IsPlayer() || entity.OwnerID.IsSteamId() || entity.skinID == 15446541672) return null;
                else return true;
            }
            return null;
        }

        private object OnNpcTarget(BaseEntity npc, CustomScientistNpc entity)
        {
            if (IsCustomScientist(entity)) return true;
            else return null;
        }

        private object CanBradleyApcTarget(BradleyAPC apc, CustomScientistNpc entity)
        {
            if (apc != null && IsCustomScientist(entity)) return false;
            else return null;
        }
		
		private void OnCorpsePopulate(ScientistNPC entity, NPCPlayerCorpse corpse)
        {
            if (entity == null || corpse == null) return;
            if (IsCustomScientist(entity))
            {
                ItemContainer container = corpse.containers[1];
                for (int i = container.itemList.Count - 1; i >= 0; i--)
                {
                    Item item = container.itemList[i];
                    item.RemoveFromContainer();
                    item.Remove();
                }
            }
        }
        #endregion Oxide Hooks

        #region Other plugins hooks
        private void CheckVersionPlugin()
        {
            webrequest.Enqueue("http://37.153.157.216:5000/Api/GetPluginVersions?pluginName=NpcSpawn", null, (code, response) =>
            {
                if (code != 200 || string.IsNullOrEmpty(response)) return;
                string[] array = response.Replace("\u0022", string.Empty).Split('.');
                VersionNumber latestVersion = new VersionNumber(Convert.ToInt32(array[0]), Convert.ToInt32(array[1]), Convert.ToInt32(array[2]));
                if (Version < latestVersion) PrintWarning($"A new version ({latestVersion}) of the plugin is available! You need to update the plugin (https://drive.google.com/drive/folders/1-18L-mG7yiGxR-PQYvd11VvXC2RQ4ZCu?usp=sharing)");
            }, this);
        }

        private object OnNpcKits(CustomScientistNpc npc)
        {
            if (IsCustomScientist(npc)) return true;
            else return null;
        }

        private object CanEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            BaseEntity attacker = info.Initiator;
            if (IsCustomScientist(entity)) return attacker != null && ((attacker as BasePlayer).IsPlayer() || attacker.skinID == 14922524 || attacker is AutoTurret || attacker is GunTrap || attacker is FlameTurret);
            if (IsCustomScientist(attacker))
            {
                if ((entity as BasePlayer).IsPlayer()) return true;
                if (entity.OwnerID.IsSteamId())
                {
                    BaseEntity weaponPrefab = info.WeaponPrefab;
                    if (weaponPrefab != null && (weaponPrefab.ShortPrefabName == "rocket_basic" || weaponPrefab.ShortPrefabName == "explosive.timed.deployed")) return true;
                }
            }
            return null;
        }

        private void SetWallFramesPos(List<Vector3> positions) => WallFrames = positions.ToHashSet();

        private void SetGeneralPos(Vector3 pos) => GeneralPosition = pos;

        private void OnCustomBarricadeSpawn(Vector3 pos) => CustomBarricades.Add(pos);

        private void OnCustomBarricadeKill(Vector3 pos) => CustomBarricades.Remove(pos);

        private void OnGeneralKill() => GeneralPosition = Vector3.zero;

        private void OnDefendableBasesEnd()
        {
            GeneralPosition = Vector3.zero;
            WallFrames.Clear();
            CustomBarricades.Clear();
        }

        internal Vector3 GeneralPosition = Vector3.zero;
        internal HashSet<Vector3> WallFrames = new HashSet<Vector3>();
        internal HashSet<Vector3> CustomBarricades = new HashSet<Vector3>();
        #endregion Other plugins hooks

        #region Find Random Points
        private readonly Dictionary<TerrainBiome.Enum, List<Vector3>> _points = new Dictionary<TerrainBiome.Enum, List<Vector3>>();
        private const int VIS_RAYCAST_LAYERS = 1 << 8 | 1 << 17 | 1 << 21;
        private const int POINT_RAYCAST_LAYERS = 1 << 4 | 1 << 8 | 1 << 10 | 1 << 15 | 1 << 16 | 1 << 21 | 1 << 23 | 1 << 27 | 1 << 28 | 1 << 29;
        private const int BLOCKED_TOPOLOGY = (int)(TerrainTopology.Enum.Cliff | TerrainTopology.Enum.Cliffside | TerrainTopology.Enum.Lake | TerrainTopology.Enum.Ocean | TerrainTopology.Enum.Monument | TerrainTopology.Enum.Offshore | TerrainTopology.Enum.River | TerrainTopology.Enum.Swamp);

        private void GenerateSpawnpoints()
        {
            for (int i = 0; i < 10000; i++)
            {
                Vector2 random = World.Size * 0.475f * UnityEngine.Random.insideUnitCircle;
                Vector3 position = new Vector3(random.x, 500f, random.y);
                if ((TerrainMeta.TopologyMap.GetTopology(position) & BLOCKED_TOPOLOGY) != 0) continue;
                float heightAtPoint;
                if (!IsPointOnTerrain(position, out heightAtPoint)) continue;
                position.y = heightAtPoint;
                TerrainBiome.Enum majorityBiome = (TerrainBiome.Enum)TerrainMeta.BiomeMap.GetBiomeMaxType(position);
                List<Vector3> list;
                if (!_points.TryGetValue(majorityBiome, out list)) _points[majorityBiome] = list = new List<Vector3>();
                list.Add(position);
            }
            foreach (KeyValuePair<TerrainBiome.Enum, List<Vector3>> dic in _points) Puts($"Points found in the biome {dic.Key}: {dic.Value.Count}");
        }

        private object GetSpawnPoint(string biomeName)
        {
            TerrainBiome.Enum biome = (TerrainBiome.Enum)Enum.Parse(typeof(TerrainBiome.Enum), biomeName, true);
            if (!_points.ContainsKey(biome)) return null;
            List<Vector3> spawnpoints = _points[biome];
            if (spawnpoints.Count == 0) return null;
            Vector3 position = spawnpoints.GetRandom();
            List<BaseEntity> list = Facepunch.Pool.GetList<BaseEntity>();
            Vis.Entities(position, 15f, list, VIS_RAYCAST_LAYERS);
            int count = list.Count;
            Facepunch.Pool.FreeList(ref list);
            if (count > 0)
            {
                spawnpoints.Remove(position);
                if (spawnpoints.Count == 0)
                {
                    GenerateSpawnpoints();
                    return null;
                }
                return GetSpawnPoint(biomeName);
            }
            return position;
        }

        private static bool IsPointOnTerrain(Vector3 position, out float heightAtPoint)
        {
            RaycastHit raycastHit;
            if (Physics.Raycast(position, Vector3.down, out raycastHit, 500f, POINT_RAYCAST_LAYERS))
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
        #endregion Find Random Points
    }
}

namespace Oxide.Plugins.NpcSpawnExtensionMethods
{
    public static class ExtensionMethods
    {
        public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return true;
            return false;
        }

        public static HashSet<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            HashSet<TSource> result = new HashSet<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) result.Add(enumerator.Current);
            return result;
        }

        public static TSource FirstOrDefault<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return enumerator.Current;
            return default(TSource);
        }

        public static List<TResult> Select<TSource, TResult>(this IList<TSource> source, Func<TSource, TResult> predicate)
        {
            List<TResult> result = new List<TResult>();
            for (int i = 0; i < source.Count; i++)
            {
                TSource element = source[i];
                result.Add(predicate(element));
            }
            return result;
        }

        public static HashSet<TResult> SelectMany<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, IEnumerable<TResult>> predicate)
        {
            HashSet<TResult> result = new HashSet<TResult>();
            foreach (TSource elements in source) foreach (TResult element in predicate(elements)) result.Add(element);
            return result;
        }

        public static HashSet<T> OfType<T>(this IEnumerable<BaseEntity> source)
        {
            HashSet<T> result = new HashSet<T>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (enumerator.Current is T) result.Add((T)(object)enumerator.Current);
            return result;
        }

        public static HashSet<TSource> ToHashSet<TSource>(this IEnumerable<TSource> source)
        {
            HashSet<TSource> result = new HashSet<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }

        public static List<TSource> ToList<TSource>(this IEnumerable<TSource> source)
        {
            List<TSource> result = new List<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }

        public static List<TSource> OrderBy<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            List<TSource> result = source.ToList();
            for (int i = 0; i < result.Count; i++)
            {
                for (int j = 0; j < result.Count - 1; j++)
                {
                    if (predicate(result[j]) > predicate(result[j + 1]))
                    {
                        TSource z = result[j];
                        result[j] = result[j + 1];
                        result[j + 1] = z;
                    }
                }
            }
            return result;
        }

        public static float Sum<TSource>(this IList<TSource> source, Func<TSource, float> predicate)
        {
            float result = 0;
            for (int i = 0; i < source.Count; i++) result += predicate(source[i]);
            return result;
        }

        public static TSource Last<TSource>(this IList<TSource> source) => source[source.Count - 1];

        public static TSource Min<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            TSource result = source.ElementAt(0);
            float resultValue = predicate(result);
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    TSource element = enumerator.Current;
                    float elementValue = predicate(element);
                    if (elementValue < resultValue)
                    {
                        result = element;
                        resultValue = elementValue;
                    }
                }
            }
            return result;
        }

        public static TSource Max<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            TSource result = source.ElementAt(0);
            float resultValue = predicate(result);
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    TSource element = enumerator.Current;
                    float elementValue = predicate(element);
                    if (elementValue > resultValue)
                    {
                        result = element;
                        resultValue = elementValue;
                    }
                }
            }
            return result;
        }

        public static TSource ElementAt<TSource>(this IEnumerable<TSource> source, int index)
        {
            int movements = 0;
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (movements == index) return enumerator.Current;
                    movements++;
                }
            }
            return default(TSource);
        }

        public static bool IsPlayer(this BasePlayer player) => player != null && player.userID.IsSteamId();

        public static bool IsExists(this BaseNetworkable entity) => entity != null && !entity.IsDestroyed;

        public static Vector3 UpPos(this Vector3 vector3) => vector3 + new Vector3(0f, 2f, 0f);
    }
}