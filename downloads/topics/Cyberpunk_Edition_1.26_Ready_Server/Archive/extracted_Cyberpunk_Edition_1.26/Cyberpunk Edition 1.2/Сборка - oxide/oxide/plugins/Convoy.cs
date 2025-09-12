using System.Collections.Generic;
using Newtonsoft.Json;
using CompanionServer.Handlers;
using UnityEngine;
using Oxide.Core.Plugins;
using Network;
using Rust;
using Rust.Modular;
using System.Collections;
using Newtonsoft.Json.Linq;
using Oxide.Game.Rust.Cui;
using Oxide.Core;
using System;
using Oxide.Plugins.ConvoyExtensionMethods;

namespace Oxide.Plugins
{
    [Info("Convoy", "Adem", "2.2.0")]
    class Convoy : RustPlugin
    {
        [PluginReference] Plugin NpcSpawn, GUIAnnouncements, DiscordMessages, PveMode, Economics, ServerRewards, IQEconomic, DynamicPVP;

        #region Variables
        const bool en = false;
        static Convoy ins;
        int pathCount = 0;

        bool active;
        bool round = true;
        int stopTime = 0;
        int destroyTime = 0;
        int eventTime = 0;
        bool destroying = false;
        bool failed = false;
        bool hackedCrate = false;

        ConvoySetting convoySetting = null;
        HashSet<string> subscribeMetods = new HashSet<string>
        {
            "OnEntityKill",
            "OnEntityTakeDamage",
            "OnVehicleModulesAssign",
            "CanHackCrate",
            "OnEntitySpawned",
            "OnHelicopterRetire",
            "CanHelicopterTarget",
            "CanBradleyApcTarget"
        };

        List<Vector3> currentPath = new List<Vector3>();
        List<ConvoyVehicle> convoyVehicles = new List<ConvoyVehicle>();
        HashSet<uint> convoyVehicleNetIds = new HashSet<uint>();
        List<ScientistNPC> freeConvoyNpc = new List<ScientistNPC>();

        HashSet<BasePlayer> players = new HashSet<BasePlayer>();
        HashSet<ulong> owners = new HashSet<ulong>();
        ulong owner;
        HashSet<BaseEntity> convoySummons = new HashSet<BaseEntity>();
        HashSet<uint> bradleyContainers = new HashSet<uint>();

        DoorCloser doorCloser;
        ConvoyTruck convoyModular;
        ConvoyHeli convoyHeli;

        HeliConfig heliConfig;
        BradleyConfig bradleyConfig;
        ModularConfig modularConfig;
        SupportModularConfig supportModularConfig;
        SedanConfig sedanConfig;

        Vector3 deathBradleyCoord = Vector3.zero;
        Vector3 deathHeliCoord = Vector3.zero;

        Coroutine stopCoroutine;
        Coroutine eventCoroutine;
        Coroutine destroyCoroutine;
        #endregion Variables

        #region API
        private bool IsConvoyVehicle(BaseEntity entity)
        {
            if (entity == null) return false;
            return convoyVehicleNetIds.Any(x => x == entity.net.ID);
        }

        private bool IsConvoyCrate(HackableLockedCrate crate)
        {
            if (crate == null) return false;
            if (convoyModular != null && convoyModular.crate != null && convoyModular.crate.net.ID == crate.net.ID) return true;
            return false;
        }

        private bool IsConvoyHeli(BaseHelicopter baseHelicopter)
        {
            if (baseHelicopter == null) return false;
            return convoyHeli != null && convoyHeli.baseHelicopter.net.ID == baseHelicopter.net.ID;
        }
        #endregion API

        #region Hooks
        void Init()
        {
            ins = this;
            UpdateConfig();
            Unsubscribes();
            Unsubscribe("OnLootSpawn");
            if (!_config.blockFirstAttack) Unsubscribe("OnExplosiveThrown");
            if (_config.betterNpcConfig.bradleyNpc) Unsubscribe("CanBradleySpawnNpc");
            if (_config.betterNpcConfig.heliNpc) Unsubscribe("CanHelicopterSpawnNpc");
            LoadData();
        }

        void OnServerInitialized()
        {
            LoadDefaultMessages();
            CheckZeroCoord();
            int vehicleCount = 0;

            Subscribe("OnLootSpawn");

            foreach (ConvoySetting convoySetting in _config.convoys)
            {
                int count = convoySetting.firstBradleyCount + convoySetting.firstModularCount + convoySetting.firstSedanCount + 1 + convoySetting.endSedanCount + convoySetting.endModularCount + convoySetting.endBradleyCount;
                if (count > vehicleCount) vehicleCount = count;
            }
            int rootCount = vehicleCount * _config.carDistance * 2 + vehicleCount + 20;

            int roadCount = _config.roadCount <= rootCount ? rootCount : _config.roadCount;

            if (_config.customRootName != "" && roots.ContainsKey(_config.customRootName))
            {
                List<List<string>> rootsPrefab = roots[_config.customRootName];
                List<List<string>> goodRoads = rootsPrefab.Where(x => x.Count > roadCount).ToList();

                if (goodRoads.Count > 0)
                {
                    List<string> currentpathString = goodRoads.GetRandom();
                    foreach (string vectorString in currentpathString) currentPath.Add(vectorString.ToVector3());
                }
            }
            if (_config.rounRoadPriority && currentPath.Count == 0)
            {
                PathList pathList = TerrainMeta.Path.Roads.FirstOrDefault(x => x.Path.Points.Length > roadCount && Vector3.Distance(x.Path.Points[0], x.Path.Points[x.Path.Points.Length - 1]) < 10f && Vector3.Distance(x.Path.Points[0], x.Path.Points[x.Path.Points.Length / 2]) > 50f);
                if (pathList != null && pathList.Path != null && pathList.Path.Points != null) currentPath = pathList.Path.Points.ToList();
            }
            if (currentPath.Count == 0)
            {
                List<PathList> goodRoads = TerrainMeta.Path.Roads.Where(x => !_config.blockRoads.Contains(TerrainMeta.Path.Roads.IndexOf(x)) && x.Path.Points.Length > roadCount && Vector3.Distance(x.Path.Points[0], x.Path.Points[x.Path.Points.Length - 1]) > 100 && UnityEngine.Physics.RaycastAll(new Ray(x.Path.Points[10] + new Vector3(0, 1, 0), Vector3.down), 4f).Any(y => y.collider.name.Contains("Road Mesh")) && UnityEngine.Physics.RaycastAll(new Ray(x.Path.Points[x.Path.Points.Length / 2] + new Vector3(0, 1, 0), Vector3.down), 4f).Any(y => y.collider.name.Contains("Road Mesh"))).ToList();
                if (goodRoads.Count > 0)
                {
                    PathList path = goodRoads.GetRandom();
                    currentPath = path.Path.Points.ToList();
                    pathCount = currentPath.Count;
                }
            }
            pathCount = currentPath.Count;

            if (pathCount == 0)
            {
                PrintError("No road detected");
                return;
            }

            if (Vector3.Distance(currentPath[0], currentPath[currentPath.Count - 1]) > 10f) round = false;

            if (_config.autoEvent && _config.convoys.Any(x => x.on && x.chance > 0))
            {
                timer.In(UnityEngine.Random.Range(_config.minStartTime, _config.maxStartTime), () =>
                {
                    if (!active) CreateConvoy();
                    else Puts("This event is active now");
                });
            }
        }

        void Unload()
        {
            if (active) DeleteConvoy(false);
            RootCar.RootStop();
            ins = null;
        }

        void OnEntitySpawned(LockedByEntCrate crate)
        {
            if (crate == null) return;

            timer.In(0.35f, () =>
            {
                if (crate == null) return;
                if (crate.ShortPrefabName == "bradley_crate" && Vector3.Distance(deathBradleyCoord, crate.transform.position) < 15f)
                {
                    if (bradleyConfig.offDelay)
                    {
                        crate.CancelInvoke(crate.Think);
                        crate.SetLocked(false);
                        crate.lockingEnt = null;
                    }
                    if (bradleyConfig.typeLootTable == 1)
                    {
                        for (int i = crate.inventory.itemList.Count - 1; i >= 0; i--)
                        {
                            Item item = crate.inventory.itemList[i];
                            item.RemoveFromContainer();
                            item.Remove();
                        }
                        AddToContainer(crate.inventory, bradleyConfig.lootTable.Items, UnityEngine.Random.Range(bradleyConfig.lootTable.Min, bradleyConfig.lootTable.Max + 1));
                    }
                }

                else if (crate.ShortPrefabName == "heli_crate" && Vector3.Distance(deathHeliCoord, crate.transform.position) < 15f)
                {
                    if (heliConfig.offDelay)
                    {
                        crate.CancelInvoke(crate.Think);
                        crate.SetLocked(false);
                        crate.lockingEnt = null;
                    }
                    if (heliConfig.typeLootTable == 1)
                    {
                        for (int i = crate.inventory.itemList.Count - 1; i >= 0; i--)
                        {
                            Item item = crate.inventory.itemList[i];
                            item.RemoveFromContainer();
                            item.Remove();
                        }
                        AddToContainer(crate.inventory, heliConfig.lootTable.Items, UnityEngine.Random.Range(heliConfig.lootTable.Min, heliConfig.lootTable.Max + 1));
                    }
                }
            });
        }

        void OnEntitySpawned(HelicopterDebris entity) => NextTick(() => { if (entity != null && !entity.IsDestroyed && deathBradleyCoord != null && (Vector3.Distance(entity.transform.position, deathBradleyCoord) < 20f || Vector3.Distance(entity.transform.position, deathHeliCoord) < 20f)) entity.Kill(); });

        object OnEntityTakeDamage(BaseVehicleModule baseVehicleModule, HitInfo info)
        {
            if (baseVehicleModule == null || info == null) return null;
            BaseModularVehicle modularVehicle = baseVehicleModule.Vehicle;
            if (modularVehicle != null && convoyVehicles.Any(x => x != null && x.baseEntity != null && x.baseEntity == modularVehicle))
            {
                if (info.InitiatorPlayer != null)
                {
                    if (!PveModeAllowAction(info.InitiatorPlayer)) return true;
                    float damageScale = convoyModular.baseEntity == modularVehicle ? modularConfig.damageMultiplier : supportModularConfig.damageMultiplier;
                    if (info.damageTypes.Has(DamageType.Explosion)) modularVehicle.health -= damageScale * info.damageTypes.Total() / 10;
                    else modularVehicle.health -= damageScale * info.damageTypes.Total() / 5;

                    if (!modularVehicle.IsDestroyed && modularVehicle.health <= 0) modularVehicle.Kill();
                    else
                    {
                        for (int i = 0; i <= modularVehicle.moduleSockets.Count; i++)
                        {
                            BaseVehicleModule module;
                            if (modularVehicle.TryGetModuleAt(i, out module)) module.SetHealth(module._maxHealth * modularVehicle.health / modularVehicle._maxHealth);
                        }
                        ConvoyTakeDamage(modularVehicle, info);
                    }
                }
                return true;
            }
            return null;
        }

        object OnEntityTakeDamage(ModularCar entity, HitInfo info)
        {
            if (convoyModular != null && convoyVehicles.Any(x => x.baseEntity == entity))
            {
                if (info.InitiatorPlayer != null)
                {
                    if (!PveModeAllowAction(info.InitiatorPlayer)) return true;
                }
                if (entity == convoyModular.baseEntity) info.damageTypes.ScaleAll(modularConfig.damageMultiplier);
                else info.damageTypes.ScaleAll(supportModularConfig.damageMultiplier);
            }
            return ConvoyTakeDamage(entity, info);
        }

        object OnEntityTakeDamage(BasicCar entity, HitInfo info)
        {
            if (info.InitiatorPlayer != null && convoySummons.Contains(entity)) return ConvoyTakeDamage(entity, info);
            return null;
        }

        object OnEntityTakeDamage(BradleyAPC entity, HitInfo info) { return ConvoyTakeDamage(entity, info); }

        object OnEntityTakeDamage(ScientistNPC entity, HitInfo info)
        {
            if (info == null || entity == null) return null;
            if ((info.InitiatorPlayer == null || !info.InitiatorPlayer.userID.IsSteamId()) && convoyVehicles.Count > 0 && convoyVehicles.Any(x => x.roamNpc.Contains(entity))) return true;
            ConvoyVehicle convoyVehicle = convoyVehicles.FirstOrDefault(x => x != null && x.scientists.Contains(entity));
            if (convoyVehicle != null) StopConvoy();
            if (convoyVehicles.Any(x => x != null && x.driver == entity)) return true;
            return null;
        }

        void OnEntityTakeDamage(BaseHelicopter entity, HitInfo info)
        {
            if (convoyHeli == null || info == null || entity == null || convoyHeli.baseHelicopter != entity) return;
            if (info.InitiatorPlayer != null) StopConvoy();
        }

        void OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null || info.Initiator == null) return;
            BradleyAPC bradleyAPC = info.Initiator as BradleyAPC;
            if (bradleyAPC != null && convoyVehicles.Any(x => x.baseEntity == bradleyAPC))
            {
                if (!player.userID.IsSteamId()) info.damageTypes.ScaleAll(0);
                else info.damageTypes.ScaleAll(bradleyConfig.scaleDamage);
            }
        }

        void OnEntityKill(ModularCar entity)
        {
            if (entity != null && convoyVehicles.Any(x => x != null && x.baseEntity == entity))
            {
                if (convoyModular != null && entity == convoyModular.baseEntity)
                {
                    failed = true;
                    if (convoyVehicles.Contains(convoyModular)) convoyVehicles.Remove(convoyModular);
                    StopConvoy();
                    if (!destroying && destroyCoroutine == null)
                    {
                        destroying = true;
                        NotifyManager.SendMessageToAll("Failed", _config.prefix);
                        NotifyManager.SendMessageToAll("PreFinish", _config.prefix, _config.preFinishTime);
                        destroyCoroutine = ServerMgr.Instance.StartCoroutine(DestroyCounter());
                    }
                }
                else ConvoyVehicleDie(entity);
            }
        }

        void OnEntityKill(BradleyAPC entity) => ConvoyVehicleDie(entity);

        void OnEntityKill(BasicCar entity) => ConvoyVehicleDie(entity);

        void OnEntityKill(BaseHelicopter entity)
        {
            if (entity == null || convoyHeli == null || convoyHeli.baseHelicopter != entity) return;
            deathHeliCoord = entity.transform.position;
            if (_config.pveMode.pve && plugins.Exists("PveMode"))
                timer.In(1f, () =>
                {
                    PveMode.Call("EventAddCrates", Name, bradleyContainers);
                });
            ConvoyVehicleDie(entity);
        }

        void OnEntityKill(BasePlayer player)
        {
            if (player == null) return;

            if (player.userID.IsSteamId() && players.Contains(player))
            {
                players.Remove(player);
                if (_config.GUI.IsGUI) CuiHelper.DestroyUi(player, "TextMain");
            }
        }

        void OnEntityDeath(ModularCar entity, HitInfo info)
        {
            if (entity == null || info == null || info.InitiatorPlayer == null) return;
            ConvoyVehicle convoyVehicle = convoyVehicles.FirstOrDefault(x => x != null && x.baseEntity != null && x.baseEntity.net.ID == entity.net.ID);
            if (convoyVehicle == null) return;
            EconomyManager.ActionEconomy(info.InitiatorPlayer.userID, "Modular");
        }

        void OnEntityDeath(BradleyAPC entity, HitInfo info)
        {
            if (entity == null || info == null || info.InitiatorPlayer == null) return;
            ConvoyVehicle convoyVehicle = convoyVehicles.FirstOrDefault(x => x != null && x.baseEntity != null && x.baseEntity.net.ID == entity.net.ID);
            if (convoyVehicle == null) return;
            EconomyManager.ActionEconomy(info.InitiatorPlayer.userID, "Bradley");
        }

        void OnEntityDeath(BasicCar entity, HitInfo info)
        {
            if (entity == null || info == null || info.InitiatorPlayer == null) return;
            ConvoyVehicle convoyVehicle = convoyVehicles.FirstOrDefault(x => x != null && x.baseEntity != null && x.baseEntity.net.ID == entity.net.ID);
            if (convoyVehicle == null) return;
            EconomyManager.ActionEconomy(info.InitiatorPlayer.userID, "Sedan");
        }

        void OnEntityDeath(BaseHelicopter entity, HitInfo info)
        {
            if (entity == null || info == null || info.InitiatorPlayer == null) return;
            if (convoySummons.Count > 0 && convoySummons.Contains(entity)) EconomyManager.ActionEconomy(info.InitiatorPlayer.userID, "Heli");
        }

        void OnEntityDeath(ScientistNPC scientistNPC, HitInfo info)
        {
            if (scientistNPC != null && info != null && info.InitiatorPlayer != null)
            {
                ConvoyVehicle convoyVehicle = convoyVehicles.FirstOrDefault(x => x != null && x.roamNpc.Contains(scientistNPC));
                if (convoyVehicle != null)
                {
                    if (_config.blockSpawnDieNpc) convoyVehicle.NpcDie(scientistNPC);
                    EconomyManager.ActionEconomy(info.InitiatorPlayer.userID, "Npc");
                }
                else if (freeConvoyNpc.Contains(scientistNPC)) EconomyManager.ActionEconomy(info.InitiatorPlayer.userID, "Npc");
            }
        }

        object CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (crate == null || player == null || convoyModular == null || convoyModular.crate == null || crate.net.ID != convoyModular.crate.net.ID) return null;
            else if (!player.InSafeZone() && (!_config.needStopConvoy || stopTime > 0) && (!_config.needKillCars || (convoySummons.Count == 0 && !convoySummons.Any(x => x != null && !x.IsDestroyed))) && (!_config.needKillNpc || (!freeConvoyNpc.Any(x => x != null && !x.IsDestroyed) && !convoyVehicles.Any(x => x != null && x.roamNpc.Any(y => y != null && !y.IsDestroyed)))))
            {
                if (_config.pveMode.pve && plugins.Exists("PveMode") && !_config.pveMode.hackCrate && PveMode.Call("CanActionEvent", Name, player) != null) return true;
                NotifyManager.SendMessageToAll("StartHackCrate", _config.prefix, player.displayName);
                if (destroying) destroyTime += (int)modularConfig.crateUnlockTime + 30;
                hackedCrate = true;
                timer.In(modularConfig.crateUnlockTime, () =>
                {
                    if (destroyCoroutine == null && !destroying)
                    {
                        destroying = true;
                        destroyCoroutine = ServerMgr.Instance.StartCoroutine(DestroyCounter());
                        NotifyManager.SendMessageToAll("PreFinish", _config.prefix, _config.preFinishTime);
                    }
                });
                if (stopTime <= 0) StopConvoy();
                timer.In(0.5f, () => crate.hackSeconds = HackableLockedCrate.requiredHackSeconds - ins.modularConfig.crateUnlockTime);
                return null;
            }
            else
            {
                NotifyManager.SendMessageToPlayer(player, "CantHackCrate", _config.prefix);
                return true;
            }
        }

        object CanMountEntity(BasePlayer player, BaseMountable entity)
        {
            BaseEntity parrent = entity.VehicleParent();
            if (parrent != null && player.userID.IsSteamId() && convoyVehicles.Any(x => x != null && x.baseEntity == parrent)) return true;
            return null;
        }

        object OnHelicopterRetire(PatrolHelicopterAI ai)
        {
            if (convoyHeli != null && convoyHeli.patrolHelicopterAI == ai) return true;
            return null;
        }

        object CanHelicopterTarget(PatrolHelicopterAI heli, BasePlayer player)
        {
            if (convoyHeli != null && heli != null && heli == convoyHeli.patrolHelicopterAI && ((_config.blockFirstAttack && stopTime == 0 && !failed && !hackedCrate) || player == null || !player.userID.IsSteamId() || player.IsSleeping())) return false;
            return null;
        }

        object CanBradleyApcTarget(BradleyAPC apc, BaseEntity entity)
        {
            if (apc != null && convoySummons.Count > 0 && convoySummons.Contains(apc))
            {
                if (_config.blockFirstAttack && stopTime == 0 && !failed && !hackedCrate) return false;
                BasePlayer player = entity as BasePlayer;
                if (player == null || !player.userID.IsSteamId() || player.IsSleeping()) return false;
            }
            return null;
        }

        object OnBotReSpawnCrateDropped(HackableLockedCrate crate)
        {
            if (active && convoyModular != null && convoyModular.crate != null && convoyModular.crate == crate) return true;
            return null;
        }

        object OnTurretTarget(AutoTurret turret, ScientistNPC scientistNPC)
        {
            if (turret == null || scientistNPC == null) return null;
            NpcConfig npcConfig = _config.NPC.FirstOrDefault(x => x.name == scientistNPC.displayName);
            if (npcConfig != null && scientistNPC.isMounted) return true;
            return null;
        }

        void OnExplosiveThrown(BasePlayer player, BaseEntity entity, ThrownWeapon item)
        {
            if (player != null && player.userID.IsSteamId() && stopTime <= 0 && convoyVehicles.Any(x => x != null && x.baseEntity != null && Vector3.Distance(player.transform.position, x.baseEntity.transform.position) < 17.5f)) StopConvoy(player);
        }

        object CanBradleySpawnNpc(BradleyAPC bradley)
        {
            if (bradley != null && convoySummons.Any(x => x != null && x.net.ID == bradley.net.ID)) return true;
            return null;
        }

        object CanHelicopterSpawnNpc(BaseHelicopter helicopter)
        {
            if (helicopter != null && convoyHeli != null && convoyHeli.baseHelicopter != null && convoyHeli.baseHelicopter.net.ID == helicopter.net.ID) return true;
            return null;
        }

        #region OtherPLugins
        void OnCorpsePopulate(BasePlayer entity, NPCPlayerCorpse corpse)
        {
            if (entity == null || corpse == null) return;
            if (entity is ScientistNPC)
            {
                NpcConfig npcConfig = _config.NPC.FirstOrDefault(x => x.name == entity.displayName);
                if (npcConfig == null) return;
                NextTick(() =>
                {
                    if (corpse == null) return;
                    ItemContainer container = corpse.containers[0];

                    if (npcConfig.typeLootTable == 0)
                    {
                        for (int i = container.itemList.Count - 1; i >= 0; i--)
                        {
                            Item item = container.itemList[i];
                            if (npcConfig.wearItems.Any(x => x.shortName == item.info.shortname))
                            {
                                item.RemoveFromContainer();
                                item.Remove();
                            }
                        }
                        if (npcConfig.deleteCorpse && corpse != null && !corpse.IsDestroyed) corpse.Kill();
                        return;
                    }

                    if (npcConfig.typeLootTable == 2 || npcConfig.typeLootTable == 3)
                    {
                        if (npcConfig.deleteCorpse && !corpse.IsDestroyed) corpse.Kill();
                        return;
                    }

                    for (int i = container.itemList.Count - 1; i >= 0; i--)
                    {
                        Item item = corpse.containers[0].itemList[i];
                        item.RemoveFromContainer();
                        item.Remove();
                    }

                    if (npcConfig.typeLootTable == 1 && npcConfig.lootTable.Min > 0) AddToContainer(container, npcConfig.lootTable.Items, UnityEngine.Random.Range(npcConfig.lootTable.Min, npcConfig.lootTable.Max + 1));

                    if (npcConfig.deleteCorpse && corpse != null && !corpse.IsDestroyed) corpse.Kill();
                });
            }
        }

        object CanPopulateLoot(LootContainer container)
        {
            if (container == null) return null;

            else if (container is HackableLockedCrate && convoyModular != null && convoyModular.crate != null && convoyModular.crate == container)
            {
                if (modularConfig.typeLootTable == 2) return null;
                else return true;
            }

            else if (container.ShortPrefabName == "bradley_crate" && Vector3.Distance(deathBradleyCoord, container.transform.position) < 15f)
            {
                bradleyContainers.Add(container.net.ID);
                if (bradleyConfig.typeLootTable == 2) return null;
                else return true;
            }

            else if (container.ShortPrefabName == "heli_crate" && Vector3.Distance(deathHeliCoord, container.transform.position) < 25f)
            {
                if (heliConfig.typeLootTable == 2) return null;
                else return true;
            }

            else return null;
        }

        object CanPopulateLoot(ScientistNPC scientistNPC, NPCPlayerCorpse corpse)
        {
            if (scientistNPC == null || corpse == null || convoyVehicles.Count == 0) return null;
            if (convoyVehicles.Any(x => x != null && x.scientists.Contains(scientistNPC)))
            {
                NpcConfig npcConfig = _config.NPC.FirstOrDefault(x => x.name == scientistNPC.name);
                if (npcConfig == null) return null;
                if (npcConfig.typeLootTable == 2) return null;
                else return true;
            }
            return null;
        }

        private object OnCustomLootContainer(uint netID)
        {
            if (!active) return null;
            if (convoyModular != null && convoyModular.baseEntity != null && !convoyModular.baseEntity.IsDestroyed && convoyModular.crate != null && convoyModular.crate.net.ID == netID)
            {
                if (modularConfig.typeLootTable == 3) return null;
                else return true;
            }

            return null;
        }

        object CanEntityTakeDamage(BasePlayer victim, HitInfo hitinfo)
        {
            if (victim == null || hitinfo == null || !_config.eventZone.isCreateZonePVP || victim == null || !victim.userID.IsSteamId() || hitinfo == null || !active || doorCloser == null || doorCloser.IsDestroyed) return null;
            BasePlayer attacker = hitinfo.InitiatorPlayer;
            if (players.Contains(victim) && (attacker == null || (attacker != null && players.Contains(attacker)))) return true;
            else return null;
        }

        object CanEntityTakeDamage(BasicCar victim, HitInfo hitinfo)
        {
            if (victim == null || hitinfo == null || active == false || hitinfo.InitiatorPlayer == null || !hitinfo.InitiatorPlayer.userID.IsSteamId()) return null;
            if (convoySummons.Contains(victim)) return true;
            else return null;
        }

        object CanEntityTakeDamage(ModularCar victim, HitInfo hitinfo)
        {
            if (victim == null || hitinfo == null || active == false || hitinfo.InitiatorPlayer == null || !hitinfo.InitiatorPlayer.userID.IsSteamId()) return null;
            if (!PveModeAllowAction(hitinfo.InitiatorPlayer)) return false;
            if (convoyVehicles.Count > 0 && convoyVehicles.Any(x => x != null && x.baseEntity != null && x.baseEntity.net.ID == victim.net.ID)) return true;
            else return null;
        }

        object CanEntityTakeDamage(BaseVehicleModule baseVehicleModule, HitInfo hitinfo)
        {
            if (baseVehicleModule == null || hitinfo == null || active == false || hitinfo.InitiatorPlayer == null || !hitinfo.InitiatorPlayer.userID.IsSteamId() || baseVehicleModule.Vehicle == null || baseVehicleModule.Vehicle.IsDestroyed) return null;
            if (!PveModeAllowAction(hitinfo.InitiatorPlayer)) return false;
            if (convoyVehicles.Any(x => x != null && x.baseEntity != null && !x.baseEntity.IsDestroyed && x.baseEntity.net.ID == baseVehicleModule.Vehicle.net.ID)) return true;
            else return null;
        }

        object OnCreateDynamicPVP(string eventName, BradleyAPC entity)
        {
            if (entity == null) return null;
            if (IsConvoyVehicle(entity)) return true;
            if (deathBradleyCoord != Vector3.zero && Vector3.Distance(deathBradleyCoord, entity.transform.position) < 20) return true;
            return null;
        }

        object OnCreateDynamicPVP(string eventName, BaseHelicopter entity)
        {
            if (entity == null) return null;
            if (IsConvoyHeli(entity)) return true;
            if (deathHeliCoord != Vector3.zero && Vector3.Distance(deathHeliCoord, entity.transform.position) < 50) return true;
            return null;
        }
        #endregion OtherPLugins
        #endregion Hooks

        #region Commands
        [ChatCommand("convoystart")]
        void StartCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin) return;
            if (active)
            {
                NotifyManager.SendMessageToPlayer(player, "EventActive", _config.prefix);
                return;
            }
            if (arg != null && arg.Length >= 1) CreateConvoy(arg[0]);
            else CreateConvoy();
        }

        [ChatCommand("convoystop")]
        void StopCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin) return;
            if (active) DeleteConvoy(true);
        }

        [ConsoleCommand("convoystart")]
        void ConsoleStartCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null) return;
            if (arg.Args != null && arg.Args.Length > 0) CreateConvoy(arg.Args[0]);
            CreateConvoy();
        }

        [ConsoleCommand("convoystartpersonal")]
        void ConsoleStartPersonalCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null) return;
            if (owners != null) owners.Clear();
            foreach (var a in arg.Args)
            {
                ulong id = Convert.ToUInt64(a);
                if (id != 0 && id.IsSteamId()) owners.Add(id);
            }
            if (arg.Args != null && arg.Args.Length > 0 && _config.convoys.Any(x => x.name == arg.Args[0])) CreateConvoy(arg.Args[0]);
            CreateConvoy();
        }

        [ConsoleCommand("convoystop")]
        void ConsoleStopCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null && active) DeleteConvoy(true);
        }

        [ChatCommand("convoyrootstart")]
        void RootStartCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin || player.isInAir) return;
            RootCar.CreateRootCar(player);
        }

        [ChatCommand("convoyrootstop")]
        void RootStopCommand(BasePlayer player, string command, string[] arg)
        {
            if (player.IsAdmin) RootCar.RootStop();
        }

        [ChatCommand("convoyrootsave")]
        void RootSaveCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin) return;

            if (arg == null || arg.Length == 0)
            {
                NotifyManager.SendMessageToPlayer(player, $"{_config.prefix} To save the route, use the command: <color=#738d43>convoyrootsave [rootpresetname]</color>");
                return;
            }

            RootCar.SaveRoot(player, arg[0]);
        }

        [ChatCommand("convoyroadblock")]
        void RoadBlockCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin || player.isInAir) return;

            PathList blockRoad = TerrainMeta.Path.Roads.FirstOrDefault(x => x.Path.Points.Any(y => Vector3.Distance(player.transform.position, y) < 10));
            if (blockRoad == null) NotifyManager.SendMessageToPlayer(player, $"{_config.prefix} Road not found <color=#ce3f27>not found</color>");
            int index = TerrainMeta.Path.Roads.IndexOf(blockRoad);
            if (_config.blockRoads.Contains(index)) NotifyManager.SendMessageToPlayer(player, $"{_config.prefix} The road is already <color=#ce3f27>blocked</color>");
            else if (blockRoad != null)
            {
                _config.blockRoads.Add(index);
                SaveConfig();
                NotifyManager.SendMessageToPlayer(player, $"{_config.prefix} The road with the index <color=#738d43>{index}</color> is <color=#ce3f27>blocked</color>");
            }
        }
        #endregion Commands

        #region Method
        void UpdateConfig()
        {
            if (_config.version != Version.ToString())
            {
                VersionNumber versionNumber;
                var versionArray = _config.version.Split('.');
                versionNumber.Major = Convert.ToInt32(versionArray[0]);
                versionNumber.Minor = Convert.ToInt32(versionArray[1]);
                versionNumber.Patch = Convert.ToInt32(versionArray[2]);
                if (versionNumber.Major == 2)
                {
                    if (versionNumber.Minor == 0)
                    {
                        if (versionNumber.Patch == 0)
                        {
                            foreach (ConvoySetting convoySetting in _config.convoys) convoySetting.displayName = "Convoy";
                        }

                        if (versionNumber.Patch <= 7)
                        {
                            foreach (NpcConfig npcConfig in _config.NPC) npcConfig.kit = "";
                        }

                        if (versionNumber.Patch <= 9)
                        {
                            _config.autoEvent = true;
                            foreach (string name in trains) if (!_config.barriers.Contains(name)) _config.barriers.Add(name);
                        }
                        versionNumber.Minor = 1;
                        versionNumber.Patch = 0;
                    }
                    if (versionNumber.Minor == 1)
                    {
                        if (versionNumber.Patch <= 5)
                        {
                            if (!_config.barriers.Contains("xmasportalentry"))
                            {
                                _config.barriers.Add("xmasportalentry");
                            }
                        }

                        if (versionNumber.Patch < 7)
                        {
                            if (!_config.barriers.Contains("stone-ore")) _config.barriers.Add("stone-ore");
                            if (!_config.barriers.Contains("metal-ore")) _config.barriers.Add("metal-ore");
                            if (!_config.barriers.Contains("sulfur-ore")) _config.barriers.Add("sulfur-ore");
                        }

                        if (versionNumber.Patch <= 9)
                        {
                            _config.pveMode.scaleDamage.Add(new ScaleDamageConfig {Type = "Helicopter", Scale = 1});
                        }

                        versionNumber.Minor = 2;
                        versionNumber.Patch = 0;
                    }

                    if (versionNumber.Minor == 2)
                    {
                    }
                }
                else
                {
                    PrintError("Delete the configuration file!");
                    NextTick(() => Server.Command($"o.unload {Name}"));
                    return;
                }
                _config.version = Version.ToString();
                SaveConfig();
            }
        }

        void Unsubscribes() { foreach (string hook in subscribeMetods) Unsubscribe(hook); }

        void Subscribes() { foreach (string hook in subscribeMetods) Subscribe(hook); }

        void CheckZeroCoord()
        {
            foreach (Collider collider in UnityEngine.Physics.OverlapSphere(Vector3.zero, 3f))
            {
                BaseEntity entity = collider.ToBaseEntity();
                if (entity == null) continue;
                if (entity.PrefabName.Contains("modular") || entity.PrefabName.Contains("locked")) entity.Kill();
            }
        }

        void CreateConvoy(string presetName = "")
        {
            if (active)
            {
                Puts("This event is active now. To finish this event (convoystop), then to start the next one");
                return;
            }

            if (!plugins.Exists("NpcSpawn"))
            {
                PrintError("NpcSpawn plugin doesn`t exist! Please read the file ReadMe.txt");
                Server.Command($"o.unload {Name}");
                return;
            }

            if (pathCount == 0)
            {
                PrintError("No road detected");
                NextTick(() => Server.Command($"o.unload {Name}"));
                return;
            }

            destroyTime = ins._config.preFinishTime;

            if (presetName != "") convoySetting = _config.convoys.FirstOrDefault(x => x.name == presetName);
            else if (_config.convoys.Any(x => x.chance > 0 && x.on))
            {
                while (convoySetting == null)
                {
                    foreach (ConvoySetting setting in _config.convoys)
                    {
                        if (setting.on && UnityEngine.Random.Range(0.0f, 100.0f) <= setting.chance) convoySetting = setting;
                    }
                }
            }

            if (convoySetting == null)
            {
                PrintError("Event configuration not found!");
                Server.Command($"o.reload {Name}");
                return;
            }

            NotifyManager.SendMessageToAll("PreStart", _config.prefix, _config.preStartTime);

            active = true;

            timer.In(_config.preStartTime, () =>
            {
                if (!active) return;

                Subscribes();
                bradleyConfig = _config.bradleyConfiguration.FirstOrDefault(x => x.presetName == convoySetting.bradleyConfigurationName);
                sedanConfig = _config.sedanConfiguration.FirstOrDefault(x => x.presetName == convoySetting.sedanConfigurationName);
                modularConfig = _config.modularConfiguration.FirstOrDefault(x => x.presetName == convoySetting.modularConfigurationName);
                heliConfig = _config.heliesConfiguration.FirstOrDefault(x => x.presetName == convoySetting.heliConfigurationName);
                supportModularConfig = _config.supportModularConfiguration.FirstOrDefault(x => x.presetName == convoySetting.supportodularConfigurationName);

                int totalVehicleCount = convoySetting.firstBradleyCount + convoySetting.firstSedanCount + convoySetting.endSedanCount + convoySetting.endBradleyCount;

                int startPoint = round ? UnityEngine.Random.Range(0, pathCount / 2) : 1;

                int count = 0;
                int totalCount = convoySetting.endBradleyCount;

                int delataPoint = _config.carDistance;

                int cycleCount = 0;
                while (cycleCount < 25 && Vector3.Distance(currentPath[0], currentPath[delataPoint]) < 10)
                {
                    delataPoint += 2;
                    cycleCount++;
                }

                for (; count < totalCount; count++)
                {
                    int firstpoint = 0, secondpoint = 0;
                    DefineNextPathPoint(startPoint, pathCount, out firstpoint, out secondpoint);
                    ConvoyBradley.CreateBradley(firstpoint, secondpoint);
                    startPoint += delataPoint;
                }

                totalCount += convoySetting.endModularCount;

                for (; count < totalCount; count++)
                {
                    int firstpoint = 0, secondpoint = 0;
                    DefineNextPathPoint(startPoint, pathCount, out firstpoint, out secondpoint);
                    ConvoyModular.CreateModular(supportModularConfig, firstpoint, secondpoint);
                    startPoint += delataPoint;
                }

                totalCount += convoySetting.endSedanCount;

                for (; count < totalCount; count++)
                {
                    int firstpoint = 0, secondpoint = 0;
                    DefineNextPathPoint(startPoint, pathCount, out firstpoint, out secondpoint);
                    ConvoySedan.CreateSedan(firstpoint, secondpoint);
                    startPoint += delataPoint;
                }

                totalCount++;

                for (; count < totalCount; count++)
                {
                    int firstpoint = 0, secondpoint = 0;
                    DefineNextPathPoint(startPoint, pathCount, out firstpoint, out secondpoint);
                    ConvoyTruck.CreateTruck(modularConfig, firstpoint, secondpoint);
                    startPoint += delataPoint;
                }

                totalCount += convoySetting.firstSedanCount;

                for (; count < totalCount; count++)
                {
                    int firstpoint = 0, secondpoint = 0;
                    DefineNextPathPoint(startPoint, pathCount, out firstpoint, out secondpoint);
                    ConvoySedan.CreateSedan(firstpoint, secondpoint);
                    startPoint += delataPoint;
                }

                totalCount += convoySetting.firstModularCount;

                for (; count < totalCount; count++)
                {
                    int firstpoint = 0, secondpoint = 0;
                    DefineNextPathPoint(startPoint, pathCount, out firstpoint, out secondpoint);
                    ConvoyModular.CreateModular(supportModularConfig, firstpoint, secondpoint);
                    startPoint += delataPoint;
                }

                totalCount += convoySetting.firstBradleyCount;

                for (; count < totalCount; count++)
                {
                    int firstpoint = 0, secondpoint = 0;
                    DefineNextPathPoint(startPoint, pathCount, out firstpoint, out secondpoint);
                    ConvoyBradley.CreateBradley(firstpoint, secondpoint);
                    startPoint += delataPoint;
                }

                timer.In(1f, () =>
                {
                    if (convoySetting.heliOn && convoyModular != null) ConvoyHeli.CreateHelicopter();
                });

                convoyVehicles.Reverse();
                NotifyManager.SendMessageToAll("EventStart", _config.prefix);

                if (eventCoroutine != null) ServerMgr.Instance.StopCoroutine(eventCoroutine);
                eventCoroutine = ServerMgr.Instance.StartCoroutine(EventCounter());

                Puts("The event has begun");

                Interface.CallHook("OnConvoyStart");
            });
        }

        void DefineNextPathPoint(int point, int pathCount, out int firstPoint, out int endPoint)
        {
            if (point > pathCount - 1)
            {
                if (round) firstPoint = point - pathCount;
                else
                {
                    PrintError("Insufficient route length!");
                    DeleteConvoy(true);
                    firstPoint = endPoint = 0;
                }
            }
            else firstPoint = point;

            int endpointClone = firstPoint++;
            if (endpointClone > pathCount - 1)
            {
                if (round) endPoint = endpointClone - pathCount;
                else
                {
                    PrintError("Insufficient route length!");
                    DeleteConvoy(true);
                    firstPoint = endPoint = 0;
                }
            }
            else endPoint = endpointClone;
        }

        void ReverseConvoy()
        {
            currentPath.Reverse();
            convoyVehicles.Reverse();
            foreach (ConvoyVehicle convoyVehicle in convoyVehicles)
            {
                if (convoyVehicle == null || convoyVehicle.baseEntity == null || convoyVehicle.baseEntity.IsDestroyed) continue;
                Transform transform = convoyVehicle.baseEntity.transform;
                convoyVehicle.rigidbody.velocity = Vector3.zero;
                transform.RotateAround(transform.position, transform.up, 180);
                convoyVehicle.Rotate();
                convoyVehicle.DefineFollowEntity();
            }
        }

        void DeleteConvoy(bool unload = false)
        {
            Unsubscribes();
            if (active) Interface.CallHook("OnConvoyStop");
            active = false;
            if (doorCloser != null && !doorCloser.IsDestroyed) doorCloser.Kill();
            foreach (BasePlayer player in ins.players) CuiHelper.DestroyUi(player, "TextMain");
            players.Clear();
            if (active) NotifyManager.SendMessageToAll("Finish", _config.prefix);
            destroying = false;
            foreach (ScientistNPC scientist in freeConvoyNpc) { if (scientist != null && !scientist.IsDestroyed) scientist.Kill(); }

            foreach (ConvoyVehicle convoyVehicle in convoyVehicles)
            {
                if (convoyVehicle != null && convoyVehicle.baseEntity != null && !convoyVehicle.baseEntity.IsDestroyed)
                {
                    convoyVehicle.destroyAll = true;
                    convoyVehicle.baseEntity.Kill();
                }
            }
            if (convoyHeli != null && convoyHeli.baseHelicopter != null && !convoyHeli.baseHelicopter.IsDestroyed) convoyHeli.baseHelicopter.Kill();
            convoyVehicles.Clear();
            convoySummons.Clear();
            Puts("The event is over");

            if (stopCoroutine != null) ServerMgr.Instance.StopCoroutine(stopCoroutine);
            if (eventCoroutine != null) ServerMgr.Instance.StopCoroutine(eventCoroutine);
            if (destroyCoroutine != null) ServerMgr.Instance.StopCoroutine(destroyCoroutine);

            if (convoyModular != null && convoyModular.baseEntity != null && !convoyModular.baseEntity.IsDestroyed) convoyModular.baseEntity.Kill();
            if (_config.pveMode.pve && plugins.Exists("PveMode")) PveMode.Call("EventRemovePveMode", Name, true);
            EconomyManager.SendBalance();
            if (owners != null) owners.Clear();
            if (unload) Server.Command($"o.reload {Name}");
        }

        void ConvoyVehicleDie(BaseEntity entity)
        {
            if (entity == null) return;
            ConvoyVehicle convoyVehicle = convoyVehicles.FirstOrDefault(x => x != null && x.baseEntity != null && !x.baseEntity.IsDestroyed && x.baseEntity.net.ID == entity.net.ID);
            if (convoyVehicle != null)
            {
                if (convoyVehicle is ConvoyBradley)
                {
                    deathBradleyCoord = entity.transform.position;

                    if (_config.pveMode.pve && plugins.Exists("PveMode"))
                        timer.In(1f, () =>
                        {
                            PveMode.Call("EventAddCrates", Name, bradleyContainers);
                            bradleyContainers.Clear();
                        });
                }
                DefineFollow(entity);
            }
            if (convoySummons.Count > 0 && convoySummons.Contains(entity))
            {
                convoySummons.Remove(entity);
                if (convoySummons.Count == 0 && convoyModular != null)
                {
                    StopConvoy();
                    convoyModular.StopMoving(false, true);
                    NotifyManager.SendMessageToAll("SecurityKill", _config.prefix);
                }
            }
        }

        object ConvoyTakeDamage(BaseEntity entity, HitInfo info)
        {
            if (entity == null || !convoyVehicles.Any(x => x != null && x.baseEntity == entity)) return null;
            if (info == null || info.InitiatorPlayer == null) return true;
            BasePlayer initiator = info.InitiatorPlayer;
            if (!initiator.userID.IsSteamId()) return true;

            if (info.ProjectileDistance >= _config.maxDamageDistance)
            {
                NotifyManager.SendMessageToPlayer(initiator, "Distance", initiator.UserIDString, _config.prefix);
                return true;
            }
            StopConvoy(initiator);
            return null;
        }

        void StopConvoy(BasePlayer initiator = null, int time = 0)
        {
            stopTime = time > 0 ? time : _config.damamageStopTime;
            if (stopCoroutine != null && stopTime != 0) return;
            stopCoroutine = ServerMgr.Instance.StartCoroutine(StopCounter());
            foreach (ConvoyVehicle convoyVehicle in convoyVehicles) if (convoyVehicle != null) convoyVehicle.StopMoving(true, true);
            if (initiator != null && convoyHeli != null) convoyHeli.SetTarget(initiator);
            if (initiator != null) NotifyManager.SendMessageToAll("ConvoyAttacked", _config.prefix, initiator.displayName);
            if (convoyModular != null) timer.In(2f, () => CreateEventZone(convoyModular.baseEntity.transform.position - new Vector3(0f, 0.5f, 0f)));
        }

        void StartConvoy()
        {
            if (failed || convoyModular == null || convoyModular.crate == null || convoyModular.crate.IsFullyHacked() || convoyModular.crate.IsBeingHacked()) return;
            if (stopCoroutine != null) ServerMgr.Instance.StopCoroutine(stopCoroutine);
            foreach (ScientistNPC scientist in freeConvoyNpc) if (scientist != null && !scientist.IsDestroyed) scientist.Kill();
            freeConvoyNpc.Clear();
            stopCoroutine = null;
            stopTime = 0;
            foreach (ConvoyVehicle convoyVehicle in convoyVehicles) convoyVehicle.StartMoving();
            if (doorCloser != null && !doorCloser.IsDestroyed) doorCloser.Kill();
            players.Clear();
            if (_config.pveMode.pve && plugins.Exists("PveMode"))
            {
                owners = (HashSet<ulong>)PveMode.Call("GetEventOwners", Name);
                owner = (ulong)PveMode.Call("GetEventOwner", Name);
                if (owners == null) owners = new HashSet<ulong>();
                PveMode.Call("EventRemovePveMode", Name, false);
            }
        }

        void CreateEventZone(Vector3 position)
        {
            if (doorCloser != null && !doorCloser.IsDestroyed) doorCloser.Kill();
            if (convoyModular == null || convoyModular.baseEntity.IsDestroyed) return;
            doorCloser = GameManager.server.CreateEntity("assets/prefabs/misc/doorcloser/doorcloser.prefab", position) as DoorCloser;
            doorCloser.gameObject.AddComponent<ZoneController>();
        }

        void DefineFollow(BaseEntity entity)
        {
            ConvoyVehicle convoyVehicle = convoyVehicles.FirstOrDefault(x => x != null && x.baseEntity.net.ID == entity.net.ID);
            if (convoyVehicle == null) return;
            int index = convoyVehicles.IndexOf(convoyVehicle);
            index++;
            if (index >= convoyVehicles.Count) return;
            ConvoyVehicle nextVehicle = convoyVehicles[index];
            if (nextVehicle == null)
            {
                convoyVehicles.Remove(nextVehicle);
                DefineFollow(entity);
                return;
            }
            BaseEntity baseEntity = nextVehicle.baseEntity;
            if (baseEntity == null || baseEntity.IsDestroyed) return;
            convoyVehicles.Remove(convoyVehicle);
            NextTick(() =>
            {
                if (nextVehicle != null && nextVehicle.baseEntity != null && !nextVehicle.baseEntity.IsDestroyed) nextVehicle.DefineFollowEntity();
            });
        }

        IEnumerator EventCounter()
        {
            while (eventTime < _config.eventTime && !destroying && active)
            {
                eventTime++;
                yield return CoroutineEx.waitForSeconds(1f);
            }
            if (!destroying && destroyCoroutine == null)
            {
                destroying = true;
                NotifyManager.SendMessageToAll("PreFinish", _config.prefix, _config.preFinishTime);
                destroyCoroutine = ServerMgr.Instance.StartCoroutine(DestroyCounter());
            }
        }

        IEnumerator StopCounter()
        {
            while (stopTime > 0)
            {
                stopTime--;
                yield return CoroutineEx.waitForSeconds(1f);
            }
            stopTime = 0;
            if (active) StartConvoy();
        }

        IEnumerator DestroyCounter()
        {
            while (destroyTime > 0)
            {
                destroyTime--;
                yield return CoroutineEx.waitForSeconds(1f);
            }
            destroyTime = 0;
            if (active) DeleteConvoy(true);
        }

        void AddToContainer(ItemContainer Container, List<ItemConfig> Items, int CountLoot)
        {
            int CountLootInContainer = 0;
            for (; CountLootInContainer <= CountLoot;)
            {
                foreach (ItemConfig item in Items)
                {
                    if (UnityEngine.Random.Range(0.0f, 100.0f) <= item.chance)
                    {
                        int amount = UnityEngine.Random.Range(item.minAmount, item.maxAmount + 1);
                        Item newItem = item.isBluePrint ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(item.shortName, amount, item.skinID);
                        if (item.isBluePrint) newItem.blueprintTarget = ItemManager.FindItemDefinition(item.shortName).itemid;
                        if (item.name != "") newItem.name = item.name;
                        if (!newItem.MoveToContainer(Container)) newItem.Remove();
                        CountLootInContainer++;
                        if (CountLootInContainer == CountLoot) return;
                    }
                }
            }
        }

        bool PveModeAllowAction(BasePlayer player)
        {
            if (!ins.plugins.Exists("PveMode") || !plugins.Exists("PveMode")) return true;
            if (ins.PveMode.Call("CanActionEvent", ins.Name, player) != null) return false;
            return true;
        }
        #endregion Method

        #region Classes 
        class ConvoyVehicle : FacepunchBehaviour
        {
            List<CoordConfig> coordNPC = new List<CoordConfig>();
            List<BaseVehicle.MountPointInfo> baseMountables = new List<BaseVehicle.MountPointInfo>();

            internal ConvoyVehicle previusVehicle;
            internal ConvoyVehicle followVehicle;
            internal Rigidbody rigidbody;
            internal BaseEntity baseEntity;
            internal ScientistNPC driver;
            internal bool destroyAll = false;
            internal bool stop = true;
            internal bool allConvoyStop = true;
            int countDieNpc = 0;
            internal List<ScientistNPC> scientists = new List<ScientistNPC>();
            internal List<ScientistNPC> roamNpc = new List<ScientistNPC>();

            NpcConfig npcConfig;

            void Awake()
            {
                Invoke(InitVehicle, 0.5f);
            }

            void InitVehicle()
            {
                baseEntity = GetComponent<BaseEntity>();
                ins.convoyVehicleNetIds.Add(baseEntity.net.ID);
                if (baseEntity is BradleyAPC)
                {
                    npcConfig = ins._config.NPC.FirstOrDefault(x => x.name == ins.bradleyConfig.npcName);
                    coordNPC = ins.bradleyConfig.coordinates;
                }
                else if (baseEntity is ModularCar)
                {
                    if (ins.convoyModular != null && ins.convoyModular.baseEntity != null && baseEntity.net.ID == ins.convoyModular.baseEntity.net.ID)
                    {
                        npcConfig = ins._config.NPC.FirstOrDefault(x => x.name == ins.modularConfig.npcName);
                        coordNPC = ins.modularConfig.coordinates;
                    }
                    else
                    {
                        npcConfig = ins._config.NPC.FirstOrDefault(x => x.name == ins.supportModularConfig.npcName);
                        coordNPC = ins.supportModularConfig.coordinates;
                    }
                }
                else if (baseEntity is BasicCar)
                {
                    npcConfig = ins._config.NPC.FirstOrDefault(x => x.name == ins.sedanConfig.npcName);
                    coordNPC = ins.sedanConfig.coordinates;
                }

                rigidbody = baseEntity.gameObject.GetComponent<Rigidbody>();
                rigidbody.mass = 3500;
                rigidbody.centerOfMass = new Vector3(0, -0.2f, 0);
                rigidbody.isKinematic = true;
                DefineMountPoints();
                StartMoving();
                Invoke(DefineFollowEntity, 0.3f);
                if (!ins.round) InvokeRepeating(CheckRotate, 0.5f, 0.1f);
                if (ins._config.deleteBarriers) InvokeRepeating(CheckBarriers, 3f, 3f);
            }

            internal virtual void Rotate() { }

            internal virtual void CheckRotate() { }

            internal virtual int GetCurrentPointIndex() { return 0; }

            internal virtual void CheckBarriers()
            {
                if (followVehicle != null) return;
                Vector3 checkPosition = baseEntity.transform.position + baseEntity.transform.forward * 4f;
                foreach (Collider collider in UnityEngine.Physics.OverlapSphere(checkPosition, 3f))
                {
                    BaseEntity entity = collider.ToBaseEntity();
                    if (entity == null || entity.IsDestroyed) continue;
                    if (ins._config.deleteTrees && entity is TreeEntity) entity.Kill();
                    else if (ins._config.barriers.Contains(entity.ShortPrefabName) && !ins.convoyVehicles.Any(x => x != null && x.baseEntity != null && x.baseEntity.net.ID == entity.net.ID))
                    {
                        if (entity is BaseVehicle)
                        {
                            BaseVehicle vehicle = entity as BaseVehicle;
                            if (!vehicle.mountPoints.Any(x => x.mountable.GetMounted() != null)) entity.Kill();
                        }
                        else entity.Kill();
                    }
                }
            }

            internal virtual void OnDestroy()
            {
                CancelInvoke(CheckRotate);
                CancelInvoke(CheckBarriers);
                KillScientists(!destroyAll);
            }

            #region Moving
            internal void DefineFollowEntity()
            {
                int index = ins.convoyVehicles.IndexOf(this);

                if (index == 0) followVehicle = null;
                else followVehicle = ins.convoyVehicles[index - 1];

                if (index >= ins.convoyVehicles.Count - 1) previusVehicle = null;
                else previusVehicle = ins.convoyVehicles[index + 1];
            }

            internal void StopMoving(bool NPC = true, bool allConvoyStop = false)
            {
                this.allConvoyStop = allConvoyStop;
                CancelInvoke(BreakOff);
                stop = true;
                BreakOn();
                if (NPC && ins.active) Invoke(CreateRoamNpc, 0.1f);
            }

            internal void StartMoving(bool delay = true)
            {
                foreach (ScientistNPC scientist in roamNpc) if (scientist != null && !scientist.IsDestroyed) scientist.Kill();
                roamNpc.Clear();

                if (allConvoyStop && ins.active) CreatePassengers();
                if (delay && ins.active) Invoke(BreakOff, 7f);
                else BreakOff();
            }

            internal void BreakOff()
            {
                allConvoyStop = false;
                rigidbody.isKinematic = false;
                stop = false;
            }

            void BreakOn() => rigidbody.isKinematic = true;
            #endregion Moving

            #region NPC
            internal void NpcDie(ScientistNPC scientistNPC)
            {
                if (!ins.active) return;
                roamNpc.Remove(scientistNPC);
                countDieNpc++;
            }

            void DefineMountPoints()
            {
                BaseVehicle baseVehicle = baseEntity.gameObject.GetComponent<BaseVehicle>();
                if (baseVehicle != null) baseMountables = baseVehicle.allMountPoints.ToList();
            }

            void CreatePassengers()
            {
                int count = baseMountables.Count;
                if (count == 0) return;
                count -= countDieNpc;
                if (count == 0) count = 1;
                driver = null;
                scientists.Clear();

                for (int i = 0; i < count; i++)
                {
                    BaseVehicle.MountPointInfo mountPointInfo = baseMountables[i];
                    BaseMountable baseMountable = mountPointInfo.mountable;
                    if (baseMountable == null) continue;
                    ScientistNPC scientist = CreateNpc(mountPointInfo.isDriver, Vector3.zero, Vector3.forward, true);
                    Invoke(() =>
                    {
                        scientist.MountObject(baseMountable);
                        baseMountable.MountPlayer(scientist);
                        if (mountPointInfo.isDriver) driver = scientist;
                        scientists.Add(scientist);
                    }, 1f);
                }
            }

            void CreateRoamNpc()
            {
                KillScientists();
                int count = coordNPC.Count - countDieNpc;
                if (count <= 0) return;

                for (int i = 0; i < count; i++)
                {
                    CoordConfig location = coordNPC[i];
                    ScientistNPC scientist = CreateNpc(false, location.position.ToVector3(), location.rotation.ToVector3(), false);
                    if (scientist != null) roamNpc.Add(scientist);
                }
            }

            ScientistNPC CreateNpc(bool driver, Vector3 position, Vector3 rotation, bool passenger)
            {
                Vector3 pos; Vector3 rot;
                ins.GetGlobal(baseEntity, position, rotation, out pos, out rot);
                return (ScientistNPC)ins.NpcSpawn.Call("SpawnNpc", pos, ins.GetNpcConfig(!driver, passenger, npcConfig));
            }

            internal void KillScientists(bool die = false)
            {
                foreach (ScientistNPC scientist in scientists)
                {
                    if (scientist != null && !scientist.IsDestroyed) scientist.Kill();
                }
                if (die)
                {
                    foreach (ScientistNPC freeScientist in roamNpc) if (freeScientist != null && !freeScientist.IsDestroyed) ins.freeConvoyNpc.Add(freeScientist);
                }
                else
                {
                    foreach (ScientistNPC freeScientist in roamNpc) if (freeScientist != null && !freeScientist.IsDestroyed) freeScientist.Kill();
                }
                roamNpc.Clear();
                scientists.Clear();
            }
            #endregion NPC
        }

        class ConvoySedan : ConvoyVehicle
        {
            internal BasicCar basicCar;
            FlasherLight flasherLight;
            internal int currentPoint = 0;
            float lastDistance = 0;

            internal static void CreateSedan(int firstPoint, int secondPoint)
            {
                Vector3 vector3 = ins.currentPath[firstPoint];
                ins.ChechGarbage(vector3);
                BasicCar car = GameManager.server.CreateEntity("assets/content/vehicles/sedan_a/sedantest.entity.prefab", vector3, Quaternion.LookRotation(ins.currentPath[firstPoint] - ins.currentPath[secondPoint])) as BasicCar;
                car.enableSaving = false;
                car.OwnerID = 755446;
                car.skinID = 755446;
                car.Spawn();
                ConvoySedan convoySedan = car.gameObject.AddComponent<ConvoySedan>();
                convoySedan.currentPoint = firstPoint;
                convoySedan.InitSedan();
                ins.convoyVehicles.Add(convoySedan);
                ins.convoySummons.Add(car);
            }

            internal void InitSedan()
            {
                basicCar = GetComponent<BasicCar>();
                basicCar.motorForceConstant = 1000;
                basicCar._maxHealth = ins.sedanConfig.hp;
                basicCar.health = ins.sedanConfig.hp;
                foreach (BasicCar.VehicleWheel vehicleWheel in basicCar.wheels) vehicleWheel.powerWheel = true;

                flasherLight = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/lights/flasherlight/electric.flasherlight.deployed.prefab") as FlasherLight;
                flasherLight.enableSaving = false;
                flasherLight.SetParent(basicCar);
                flasherLight.transform.localPosition = new Vector3(0.45f, 1.64f, 0.4f);
                flasherLight.Spawn();
                flasherLight.UpdateFromInput(1, 0);
                InvokeRepeating(UpdateFlasher, 10, 10);
            }

            internal override void CheckRotate()
            {
                if (followVehicle != null) return;
                if (currentPoint >= ins.pathCount - 3) ins.ReverseConvoy();
                if (Physics.RaycastAll(transform.position, basicCar.transform.forward, 7.5f, 1 << 16).Any(x => x.collider != null && x.collider.name.Contains("cliff"))) ins.ReverseConvoy();
            }

            internal override int GetCurrentPointIndex()
            {
                return currentPoint;
            }

            internal override void Rotate() => currentPoint = ins.pathCount - currentPoint;

            void FixedUpdate()
            {
                if (allConvoyStop || ins.failed) return;
                if (currentPoint == ins.pathCount - 1) currentPoint = 0;
                if (ins.round)
                {
                    if (currentPoint >= ins.pathCount - 2) currentPoint = 0;
                }
                else if (currentPoint >= ins.pathCount)
                {
                    ins.ReverseConvoy();
                    return;
                }
                Vector3 nextPoint = ins.currentPath[currentPoint + 1];
                float destanationDistance = Vector3.Distance(new Vector3(basicCar.transform.position.x, 0, basicCar.transform.position.z), new Vector3(nextPoint.x, 0, nextPoint.z));

                if (destanationDistance < 6f)
                {
                    lastDistance = 0;
                    currentPoint++;
                }

                if (rigidbody.velocity.magnitude < 0.5f)
                {
                    if (lastDistance > 0 && lastDistance - destanationDistance < -0.0f)
                    {
                        rigidbody.isKinematic = false;
                        rigidbody.AddForce(new Vector3(basicCar.transform.forward.x, 0, basicCar.transform.forward.z) * (rigidbody.velocity.magnitude + 0.1f), ForceMode.VelocityChange);
                        lastDistance = 0;
                    }
                }
                lastDistance = destanationDistance;
                basicCar.SetFlag(BaseEntity.Flags.Reserved2, true);
                ControlTurn();
                ControlTrottle();
            }

            void UpdateFlasher()
            {
                flasherLight.limitNetworking = true;
                flasherLight.limitNetworking = false;
            }

            void SetSpeed(float gasP)
            {
                if (allConvoyStop) return;
                float maxSpeed = followVehicle == null ? 4 : 6;

                if (gasP < 0 && !stop)
                {
                    StopMoving(false);
                    basicCar.brakePedal = 100;
                    return;
                }

                else if (gasP > 0 && stop)
                {
                    StartMoving(false);
                    basicCar.brakePedal = 0;
                }

                else if (rigidbody.velocity.magnitude > maxSpeed)
                {
                    if (rigidbody.velocity.magnitude > ++maxSpeed) basicCar.brakePedal = 50;
                    basicCar.gasPedal = 0;
                }

                else
                {
                    basicCar.gasPedal = gasP;
                    basicCar.brakePedal = 0;
                }

                basicCar.motorForceConstant = gasP;
                rigidbody.isKinematic = false;
            }

            void ControlTrottle()
            {
                if (previusVehicle != null && previusVehicle.baseEntity != null && !previusVehicle.baseEntity.IsDestroyed && Vector3.Distance(previusVehicle.baseEntity.transform.position, baseEntity.transform.position) > 35) SetSpeed(-1);
                else
                {
                    if (followVehicle == null || followVehicle.baseEntity == null || followVehicle.baseEntity.IsDestroyed) SetSpeed(80);
                    else
                    {
                        float distance = Vector3.Distance(basicCar.transform.position, followVehicle.baseEntity.transform.position);
                        SetSpeed(ins.GetSpeed(10, distance, 100, 1.1f));
                    }
                }
            }

            void ControlTurn()
            {
                float turning = 0;

                Vector3 lhs = global::BradleyAPC.Direction2D(ins.currentPath[currentPoint + 1], basicCar.transform.position);
                float num2 = Vector3.Dot(lhs, basicCar.transform.right);
                float num3 = Vector3.Dot(lhs, basicCar.transform.right);
                float num4 = Vector3.Dot(lhs, -basicCar.transform.right);

                if (Vector3.Dot(lhs, -basicCar.transform.forward) > num2)
                {
                    if (num3 >= num4) turning = 1f;
                    else turning = -1f;
                }
                else turning = Mathf.Clamp(num2 * 3f, -1f, 1f);
                if (rigidbody.velocity.magnitude < 0.6f) turning = 0;

                basicCar.steering = turning * 70;
                basicCar.DoSteering();
            }
        }

        class ConvoyTruck : ConvoyModular
        {
            ModularConfig modularConfig;
            MapMarkerGenericRadius mapmarker;
            VendingMachineMapMarker vendingMarker;
            internal HackableLockedCrate crate;

            internal static void CreateTruck(ModularConfig modularConfig, int firstPoint, int secondPoint)
            {
                ModularCar car = CreateModularCar(firstPoint, secondPoint, modularConfig.prefabName);
                ConvoyTruck convoyTruck = car.gameObject.AddComponent<ConvoyTruck>();
                convoyTruck.InitTruck(car, modularConfig, firstPoint);
                ins.convoyVehicles.Add(convoyTruck);
                ins.convoyModular = convoyTruck;
            }

            void InitTruck(ModularCar modularCar, ModularConfig modularConfig, int currentPoint)
            {
                this.modularConfig = modularConfig;
                base.InitModular(modularCar, modularConfig, currentPoint);
                Invoke(CreateCrate, 0.5f);
            }

            internal override void OnDestroy()
            {
                base.OnDestroy();
                CancelInvoke(UpdateMapMarker);
                if (mapmarker.IsExists()) mapmarker.Kill();
                if (vendingMarker.IsExists()) vendingMarker.Kill();
                CancelInvoke(UpdateCrate);
            }

            void CreateCrate()
            {
                SpawnCrate();
                InvokeRepeating(UpdateCrate, 10f, 10f);
                if (ins._config.marker.IsMarker) SpawnMapMarker();
            }

            internal void SpawnCrate()
            {
                crate = GameManager.server.CreateEntity("assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", modularCar.transform.InverseTransformPoint(ins.modularConfig.crateLocation.position.ToVector3())) as HackableLockedCrate;
                crate.SetParent(modularCar, false, true);
                crate.transform.localPosition = ins.modularConfig.crateLocation.position.ToVector3();
                crate.transform.localEulerAngles = ins.modularConfig.crateLocation.rotation.ToVector3();
                crate.Spawn();
                Rigidbody crateRigidbody = crate.GetComponent<Rigidbody>();
                Destroy(crateRigidbody);

                if (ins.modularConfig.typeLootTable == 1)
                {
                    Invoke(() =>
                    {
                        crate.inventory.capacity = ins.modularConfig.lootTable.Max;
                        crate.hackSeconds = HackableLockedCrate.requiredHackSeconds - ins.modularConfig.crateUnlockTime;
                        for (int i = crate.inventory.itemList.Count - 1; i >= 0; i--)
                        {
                            Item item = crate.inventory.itemList[i];
                            item.RemoveFromContainer();
                            item.Remove();
                        }
                        ins.AddToContainer(crate.inventory, ins.modularConfig.lootTable.Items, UnityEngine.Random.Range(ins.modularConfig.lootTable.Min, ins.modularConfig.lootTable.Max + 1));
                    }, 0.1f);
                }
                crate.EnableGlobalBroadcast(true);
                crate.syncPosition = true;
                crate.SendNetworkUpdate();
            }

            void UpdateMapMarker()
            {
                if (mapmarker != null)
                {
                    mapmarker.transform.position = modularCar.transform.position;
                    mapmarker.SendUpdate();
                    mapmarker.SendNetworkUpdate();
                }

                if (vendingMarker != null)
                {
                    vendingMarker.transform.position = modularCar.transform.position;
                    vendingMarker.markerShopName = $"{ins.convoySetting.displayName} ({NotifyManager.GetTimeMessage(null, ins._config.eventTime - ins.eventTime + ins.destroyTime)})";
                    vendingMarker.SendNetworkUpdate();
                }
            }

            void UpdateCrate()
            {
                if (crate == null || crate.IsDestroyed) return;
                crate.SendNetworkUpdate();
            }

            void SpawnMapMarker()
            {
                mapmarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", modularCar.transform.position) as MapMarkerGenericRadius;
                mapmarker.enableSaving = false;
                mapmarker.Spawn();
                mapmarker.radius = ins._config.marker.Radius;
                mapmarker.alpha = ins._config.marker.Alpha;
                mapmarker.color1 = new Color(ins._config.marker.Color1.r, ins._config.marker.Color1.g, ins._config.marker.Color1.b);
                mapmarker.color2 = new Color(ins._config.marker.Color2.r, ins._config.marker.Color2.g, ins._config.marker.Color2.b);

                vendingMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", modularCar.transform.position) as VendingMachineMapMarker;
                vendingMarker.Spawn();
                vendingMarker.markerShopName = $"Convoy ({ins._config.eventTime - ins.eventTime + ins.destroyTime} s)";

                InvokeRepeating(UpdateMapMarker, 0, 1f);
            }
        }

        class ConvoyModular : ConvoyVehicle
        {
            SupportModularConfig supportModularConfig;
            internal ModularCar modularCar;
            internal int currentPoint = 0;
            float lastDistance = 0;

            internal static void CreateModular(SupportModularConfig supportModularConfig, int firstPoint, int secondPoint)
            {
                ModularCar car = CreateModularCar(firstPoint, secondPoint, ins.supportModularConfig.prefabName);
                ConvoyModular modular = car.gameObject.AddComponent<ConvoyModular>();
                modular.baseEntity = car;
                modular.currentPoint = firstPoint;
                ins.convoyVehicles.Add(modular);
                modular.InitModular(car, supportModularConfig, firstPoint);
                ins.convoySummons.Add(car);
            }

            internal static ModularCar CreateModularCar(int firstPoint, int secondPoint, string presetName)
            {
                Vector3 vector3 = ins.currentPath[firstPoint];
                ins.ChechGarbage(vector3);
                ModularCar car = GameManager.server.CreateEntity(presetName, vector3, Quaternion.LookRotation(ins.currentPath[firstPoint] - ins.currentPath[secondPoint])) as ModularCar;
                car.enableSaving = false;
                car.spawnSettings.useSpawnSettings = false;
                car.OwnerID = 755446;
                car.skinID = 755446;
                car.Spawn();
                return car;
            }

            protected void InitModular(ModularCar modularCar, SupportModularConfig supportModularConfig, int currentPoint)
            {
                this.supportModularConfig = supportModularConfig;
                this.modularCar = modularCar;
                this.currentPoint = currentPoint;
                modularCar = GetComponent<ModularCar>();

                Invoke(Build, 0.1f);
            }

            internal override void OnDestroy()
            {
                base.OnDestroy();
            }

            internal override int GetCurrentPointIndex()
            {
                return currentPoint;
            }

            internal override void CheckRotate()
            {
                if (followVehicle != null) return;
                if (currentPoint >= ins.pathCount - 3) ins.ReverseConvoy();

                if (Physics.RaycastAll(transform.position, modularCar.transform.forward, 7.5f, 1 << 16).Any(x => x.collider != null && x.collider.name.Contains("cliff"))) ins.ReverseConvoy();
            }

            internal override void Rotate() => currentPoint = ins.pathCount - currentPoint;

            #region Builder
            void Build()
            {
                AddCarModules();
                modularCar.GetFuelSystem().cachedHasFuel = true;
                modularCar.GetFuelSystem().nextFuelCheckTime = float.MaxValue;
            }

            void AddCarModules()
            {
                List<string> modules = supportModularConfig.modules;
                for (int socketIndex = 0; socketIndex < modularCar.TotalSockets && socketIndex < modules.Count; socketIndex++)
                {
                    string shortName = modules[socketIndex];
                    if (shortName == "") continue;
                    Item existingItem = modularCar.Inventory.ModuleContainer.GetSlot(socketIndex);
                    if (existingItem != null) continue;
                    Item moduleItem = ItemManager.CreateByName(shortName);
                    if (moduleItem == null) continue;
                    moduleItem.conditionNormalized = 100;

                    if (!modularCar.TryAddModule(moduleItem, socketIndex)) moduleItem.Remove();
                }

                Invoke(AddEngineParts, 1f);
            }

            void AddEngineParts()
            {
                foreach (BaseVehicleModule module in modularCar.AttachedModuleEntities)
                {
                    VehicleModuleEngine engineModule = module as VehicleModuleEngine;
                    if (engineModule == null) continue;
                    engineModule.engine.maxFuelPerSec = 0;
                    engineModule.engine.idleFuelPerSec = 0;
                    EngineStorage engineStorage = engineModule.GetContainer() as EngineStorage;
                    if (engineStorage == null) continue;
                    engineStorage.dropsLoot = false;
                    ItemContainer inventory = engineStorage.inventory;
                    for (int i = 0; i < inventory.capacity; i++)
                    {
                        ItemModEngineItem output;
                        if (!engineStorage.allEngineItems.TryGetItem(1, engineStorage.slotTypes[i], out output)) continue;
                        ItemDefinition component = output.GetComponent<ItemDefinition>();
                        Item item = ItemManager.Create(component);
                        if (item == null) continue;
                        item._maxCondition = int.MaxValue;
                        item.condition = int.MaxValue;
                        item.MoveToContainer(engineStorage.inventory, i, allowStack: false);
                    }
                    engineModule.RefreshPerformanceStats(engineStorage);
                    return;
                }
            }
            #endregion Builder

            #region Moving
            void FixedUpdate()
            {
                if (allConvoyStop || ins.failed) return;
                if (modularCar.engineController.IsOff && !modularCar.engineController.CanRunEngine())
                {
                    AddCarModules();
                    modularCar.GetFuelSystem().cachedHasFuel = true;
                    modularCar.GetFuelSystem().nextFuelCheckTime = float.MaxValue;
                }
                if (modularCar.engineController.IsOff && !modularCar.engineController.IsStarting && driver != null) modularCar.engineController.TryStartEngine(driver);

                if (ins.round)
                {
                    if (currentPoint >= ins.pathCount - 2) currentPoint = 0;
                }
                else if (currentPoint >= ins.pathCount)
                {
                    ins.ReverseConvoy();
                    return;
                }

                Vector3 nextPint = ins.currentPath[currentPoint + 1];
                float destanationDistance = Vector3.Distance(new Vector3(modularCar.transform.position.x, 0, modularCar.transform.position.z), new Vector3(nextPint.x, 0, nextPint.z));

                if (destanationDistance < 6f)
                {
                    currentPoint++;
                    lastDistance = 0;
                }

                if (rigidbody.velocity.magnitude < 1f)
                {
                    if (lastDistance > 0 && lastDistance - destanationDistance < -0.0f)
                    {
                        rigidbody.isKinematic = false;
                        rigidbody.AddForce(new Vector3(modularCar.transform.forward.x, 0, modularCar.transform.forward.z) * (rigidbody.velocity.magnitude + 0.5f), ForceMode.VelocityChange);
                        lastDistance = 0;
                    }
                    lastDistance = destanationDistance;
                }

                modularCar.SetFlag(ModularCar.Flags.Reserved5, true);
                ControlTrottle();
                ControlTurn();
            }

            InputState CreateInput()
            {
                InputState inputState = new InputState();
                inputState.previous.mouseDelta = new Vector3(0, 0, 0);
                inputState.current.aimAngles = new Vector3(0, 0, 0);
                inputState.current.mouseDelta = new Vector3(0, 0, 0);
                return inputState;
            }

            void SetSpeed(float gasP)
            {
                if (allConvoyStop) return;

                float maxSpeed = followVehicle == null ? 4 : 6;

                if (gasP < 0 && !stop)
                {
                    StopMoving(false);
                    return;
                }

                else if (gasP > 0 && stop) StartMoving(false);

                else if (rigidbody.velocity.magnitude > maxSpeed)
                {
                    if (rigidbody.velocity.magnitude > ++maxSpeed) gasP = -0.3f;
                    else gasP = 0;
                }

                rigidbody.AddForce(new Vector3(modularCar.transform.forward.x, 0, modularCar.transform.forward.z) * gasP, ForceMode.VelocityChange);
            }

            void ControlTrottle()
            {
                if (previusVehicle != null && previusVehicle.baseEntity != null && !previusVehicle.baseEntity.IsDestroyed && Vector3.Distance(previusVehicle.baseEntity.transform.position, baseEntity.transform.position) > 35) SetSpeed(-1);
                else
                {
                    if (followVehicle == null || followVehicle.baseEntity == null || followVehicle.baseEntity.IsDestroyed) SetSpeed(0.5f);
                    else
                    {
                        float distance = Vector3.Distance(modularCar.transform.position, followVehicle.transform.position);
                        SetSpeed(ins.GetSpeed(0.3f, distance, 1.5f, 1.05f));
                    }
                }
            }

            void ControlTurn()
            {
                float turning;

                Vector3 lhs = global::BradleyAPC.Direction2D(ins.currentPath[currentPoint + 1], modularCar.transform.position);
                float num2 = Vector3.Dot(lhs, modularCar.transform.right);
                float num3 = Vector3.Dot(lhs, modularCar.transform.right);
                float num4 = Vector3.Dot(lhs, -modularCar.transform.right);

                if (Vector3.Dot(lhs, -modularCar.transform.forward) > num2)
                {
                    if (num3 >= num4) turning = 1f;
                    else turning = -1f;
                }
                else turning = Mathf.Clamp(num2 * 3f, -1f, 1f);

                InputState inputState = CreateInput();
                if (turning < -0.5f) inputState.current.buttons = 8;

                else if (turning > 0.5f) inputState.current.buttons = 16;
                else inputState.current.buttons = 0;

                if (rigidbody.velocity.magnitude < 0.3f) inputState.current.buttons = 0;

                if (driver != null && inputState != null) modularCar.PlayerServerInput(inputState, driver);
            }
            #endregion Moving
        }

        class ConvoyBradley : ConvoyVehicle
        {
            internal BradleyAPC bradley;
            bool init = false;

            internal static void CreateBradley(int firstPoint, int secondPoint)
            {
                Vector3 vector3 = ins.currentPath[firstPoint];
                ins.ChechGarbage(vector3);
                BradleyAPC bradley = GameManager.server.CreateEntity("assets/prefabs/npc/m2bradley/bradleyapc.prefab", vector3, Quaternion.LookRotation(ins.currentPath[firstPoint] - ins.currentPath[secondPoint])) as BradleyAPC;
                bradley.OwnerID = 755446;
                bradley.skinID = 755446;
                bradley.Spawn();
                bradley.ClearPath();
                bradley.currentPath = ins.currentPath;
                bradley.currentPathIndex = firstPoint;
                ConvoyBradley convoyBradley = bradley.gameObject.AddComponent<ConvoyBradley>();
                ins.convoyVehicles.Add(convoyBradley);
                convoyBradley.InitBradley();
                ins.convoySummons.Add(bradley);
            }

            internal void InitBradley()
            {
                bradley = GetComponent<BradleyAPC>();
                bradley.pathLooping = true;
                bradley._maxHealth = ins.bradleyConfig.hp;
                bradley.health = ins.bradleyConfig.hp;
                bradley.maxCratesToSpawn = ins.bradleyConfig.countCrates;
                bradley.viewDistance = ins.bradleyConfig.viewDistance;
                bradley.searchRange = ins.bradleyConfig.searchDistance;
                bradley.coaxAimCone *= ins.bradleyConfig.coaxAimCone;
                bradley.coaxFireRate *= ins.bradleyConfig.coaxFireRate;
                bradley.coaxBurstLength = ins.bradleyConfig.coaxBurstLength;
                bradley.nextFireTime = ins.bradleyConfig.nextFireTime;
                bradley.topTurretFireRate = ins.bradleyConfig.topTurretFireRate;
                bradley.currentPath = ins.currentPath;
                bradley.enableSaving = false;
            }

            internal override int GetCurrentPointIndex()
            {
                return bradley.currentPathIndex;
            }

            internal override void CheckRotate()
            {
                if (followVehicle != null) return;
                if (bradley.currentPathIndex >= ins.pathCount - 3) ins.ReverseConvoy();
                if (Physics.RaycastAll(transform.position, bradley.transform.forward, 7.5f, 1 << 16).Any(x => x.collider != null && x.collider.name.Contains("cliff"))) ins.ReverseConvoy();
            }

            internal override void Rotate()
            {
                bradley.currentPath = ins.currentPath;
                bradley.currentPathIndex = ins.pathCount - bradley.currentPathIndex;
            }

            #region Moving
            void FixedUpdate()
            {
                if (ins.round && bradley.currentPathIndex >= ins.pathCount - 3) bradley.currentPathIndex = 1;

                Vector3 nextPint = ins.currentPath[bradley.currentPathIndex];
                float destanationDistance = Vector3.Distance(new Vector3(bradley.transform.position.x, 0, bradley.transform.position.z), new Vector3(nextPint.x, 0, nextPint.z));

                if (destanationDistance < 6f) bradley.currentPathIndex++;

                if (!init && rigidbody != null && !rigidbody.isKinematic)
                {
                    rigidbody.AddForce(bradley.transform.forward * 5000, ForceMode.Force);
                    if (rigidbody.velocity.magnitude > 2) init = true;
                }

                if (previusVehicle != null && previusVehicle.baseEntity != null && !previusVehicle.baseEntity.IsDestroyed && Vector3.Distance(previusVehicle.baseEntity.transform.position, baseEntity.transform.position) > 35)
                {
                    SetSpeed(-1);
                }
                else
                {
                    if (followVehicle == null || followVehicle.baseEntity == null || followVehicle.baseEntity.IsDestroyed) SetSpeed(800);
                    else
                    {
                        float distance = Vector3.Distance(bradley.transform.position, followVehicle.baseEntity.transform.position);
                        SetSpeed(ins.GetSpeed(200, distance, 2000, 1f));
                    }
                }
                bradley.SetFlag(BradleyAPC.Flags.Reserved5, true);
            }

            void SetSpeed(float gasP)
            {
                if (allConvoyStop) return;

                float maxSpeed = followVehicle == null ? 4 : 7.5f;

                if (gasP < 0 && !stop)
                {
                    StopMoving(false);
                    return;
                }

                else if (gasP > 0 && stop)
                {
                    StartMoving(false);
                }

                else if (rigidbody.velocity.magnitude > maxSpeed)
                {
                    bradley.leftThrottle = 0;
                    bradley.rightThrottle = 0;
                    bradley.moveForceMax = 0;
                }
                else bradley.moveForceMax = gasP;
            }
            #endregion Moving
        }

        class ConvoyHeli : FacepunchBehaviour
        {
            BaseEntity targetEntity;

            internal PatrolHelicopterAI patrolHelicopterAI;
            internal BaseHelicopter baseHelicopter;

            internal static void CreateHelicopter()
            {
                Vector3 position = ins.convoyModular.baseEntity.transform.position + new Vector3(0, ins.heliConfig.height, 0);
                Quaternion rotation = ins.convoyModular.baseEntity.transform.rotation;
                BaseHelicopter heli = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab", position, rotation) as BaseHelicopter;
                heli.enableSaving = false;
                heli.OwnerID = 755446;
                heli.skinID = 755446;
                heli.Spawn();
                heli.transform.position = position;
                ins.convoyHeli = heli.gameObject.AddComponent<ConvoyHeli>();
                heli._maxHealth = ins.heliConfig.hp;
                heli.startHealth = ins.heliConfig.hp;
                ins.convoyHeli.InitHelicopter(ins.heliConfig.hp);
                ins.convoySummons.Add(heli);
            }

            internal void InitHelicopter(float hp)
            {
                baseHelicopter = GetComponent<BaseHelicopter>();
                patrolHelicopterAI = baseHelicopter.GetComponent<PatrolHelicopterAI>();
                baseHelicopter.startHealth = hp;
                baseHelicopter.InitializeHealth(hp, hp);
                baseHelicopter.maxCratesToSpawn = ins.heliConfig.cratesAmount;
                baseHelicopter.bulletDamage = ins.heliConfig.bulletDamage;
                baseHelicopter.bulletSpeed = ins.heliConfig.bulletSpeed;
                var weakspots = baseHelicopter.weakspots;
                if (weakspots != null && weakspots.Length > 1)
                {
                    weakspots[0].maxHealth = ins.heliConfig.mainRotorHealth;
                    weakspots[0].health = ins.heliConfig.mainRotorHealth;
                    weakspots[1].maxHealth = ins.heliConfig.rearRotorHealth;
                    weakspots[1].health = ins.heliConfig.rearRotorHealth;
                }
                targetEntity = ins.convoyModular.baseEntity;
                patrolHelicopterAI.isRetiring = true;
            }

            internal void SetTarget(BasePlayer player)
            {
                patrolHelicopterAI.SetTargetDestination(player.transform.position);
                patrolHelicopterAI._targetList.Add(new PatrolHelicopterAI.targetinfo(player, player));
                patrolHelicopterAI.State_Strafe_Enter(player.transform.position);
            }

            void FixedUpdate()
            {

                if (targetEntity == null || targetEntity.IsDestroyed) return;
                if (ins.stopTime <= 0)
                {
                    patrolHelicopterAI.SetTargetDestination(targetEntity.transform.position + new Vector3(0, ins.heliConfig.height, 0));
                    if (Vector2.Distance(new Vector2(baseHelicopter.transform.position.x, baseHelicopter.transform.position.z), new Vector2(targetEntity.transform.position.x, targetEntity.transform.position.z)) < 35) patrolHelicopterAI.SetIdealRotation(targetEntity.transform.rotation, 100);
                }
                else if (targetEntity.Distance(baseHelicopter.transform.position) > ins.heliConfig.distance)
                {
                    patrolHelicopterAI.SetTargetDestination(targetEntity.transform.position + new Vector3(0, ins.heliConfig.height, 0));
                }
            }

            public void OnDestroy()
            {
                if (baseHelicopter != null && !baseHelicopter.IsDestroyed) baseHelicopter.Kill();
            }
        }

        class ZoneController : FacepunchBehaviour
        {
            DoorCloser mainCloser;
            SphereCollider sphereCollider;
            HashSet<BaseEntity> spheres = new HashSet<BaseEntity>();

            Coroutine guiCoroune;

            void Awake()
            {
                gameObject.layer = (int)Layer.Reserved1;
                sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.isTrigger = true;
                sphereCollider.radius = ins._config.eventZone.radius;
                mainCloser = GetComponent<DoorCloser>();
                if (ins._config.pveMode.pve && ins.plugins.Exists("PveMode"))
                {
                    Invoke(() =>
                    {
                        JObject config = new JObject
                        {
                            ["Damage"] = ins._config.pveMode.damage,
                            ["ScaleDamage"] = new JArray { ins._config.pveMode.scaleDamage.Select(x => new JObject { ["Type"] = x.Type, ["Scale"] = x.Scale }) },
                            ["LootCrate"] = ins._config.pveMode.lootCrate,
                            ["HackCrate"] = ins._config.pveMode.hackCrate,
                            ["LootNpc"] = ins._config.pveMode.lootNpc,
                            ["DamageNpc"] = ins._config.pveMode.damageNpc,
                            ["DamageTank"] = ins._config.pveMode.damageTank,
                            ["DamageHelicopter"] = ins._config.pveMode.damageHeli,
                            ["TargetNpc"] = ins._config.pveMode.targetNpc,
                            ["TargetTank"] = ins._config.pveMode.targetTank,
                            ["TargetHelicopter"] = ins._config.pveMode.targetHeli,
                            ["CanEnter"] = ins._config.pveMode.canEnter,
                            ["CanEnterCooldownPlayer"] = ins._config.pveMode.canEnterCooldownPlayer,
                            ["TimeExitOwner"] = ins._config.pveMode.timeExitOwner,
                            ["AlertTime"] = ins._config.pveMode.alertTime,
                            ["RestoreUponDeath"] = ins._config.pveMode.restoreUponDeath,
                            ["CooldownOwner"] = ins._config.pveMode.cooldownOwner,
                            ["Darkening"] = ins._config.pveMode.darkening
                        };
                        HashSet<uint> npcs = new HashSet<uint>();
                        HashSet<uint> bradleys = new HashSet<uint>();
                        HashSet<uint> helicopters = new HashSet<uint>();
                        BasePlayer playerOwner = null;
                        if (ins.owner != 0)
                        {
                            playerOwner = BasePlayer.activePlayerList.FirstOrDefault(x => x.userID == ins.owner);
                        }
                        if (ins.convoyHeli != null && ins.convoyHeli.baseHelicopter != null) helicopters.Add(ins.convoyHeli.baseHelicopter.net.ID);
                        foreach (ConvoyVehicle convoyVehicle in ins.convoyVehicles)
                        {
                            foreach (ScientistNPC scientistNPC in convoyVehicle.roamNpc) npcs.Add(scientistNPC.net.ID);
                            if (convoyVehicle is ConvoyBradley) bradleys.Add(convoyVehicle.baseEntity.net.ID);
                        }
                        ins.PveMode.Call("EventAddPveMode", ins.Name, config, mainCloser.transform.position, ins._config.eventZone.radius, new HashSet<uint> { ins.convoyModular.crate.net.ID }, npcs, bradleys, helicopters, ins.owners, playerOwner);
                    }, 1f);
                }
                else if (ins._config.eventZone.isDome) CreateSphere();
                if (ins._config.GUI.IsGUI) guiCoroune = ServerMgr.Instance.StartCoroutine(GuiCoroune());
            }

            void CreateSphere()
            {
                for (int i = 0; i < ins._config.eventZone.darkening; i++)
                {
                    BaseEntity sphere = GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", mainCloser.transform.position);
                    SphereEntity entity = sphere.GetComponent<SphereEntity>();
                    entity.currentRadius = ins._config.eventZone.radius * 2;
                    entity.lerpSpeed = 0f;
                    sphere.enableSaving = false;
                    sphere.Spawn();
                    spheres.Add(sphere);
                }
            }

            IEnumerator GuiCoroune()
            {
                while (true)
                {
                    int time = ins._config.eventTime - ins.eventTime;
                    if (ins.destroying) time = ins.destroyTime;
                    foreach (BasePlayer player in ins.players) MessageGUI(player, GetMessage("GUI", player.UserIDString, NotifyManager.GetTimeMessage(player.UserIDString, time)));
                    yield return CoroutineEx.waitForSeconds(1f);
                }
            }

            void OnTriggerEnter(Collider other)
            {
                BasePlayer player = other.GetComponentInParent<BasePlayer>();
                if (player != null && player.userID.IsSteamId())
                {
                    int time = ins._config.eventTime - ins.eventTime;
                    if (ins.destroying) time = ins.destroyTime;
                    ins.players.Add(player);
                    if (ins._config.GUI.IsGUI) MessageGUI(player, GetMessage("GUI", player.UserIDString, NotifyManager.GetTimeMessage(player.UserIDString, time)));
                    if (ins._config.eventZone.isCreateZonePVP) NotifyManager.SendMessageToPlayer(player, "EnterPVP", ins._config.prefix);
                }
            }

            void OnTriggerExit(Collider other)
            {
                BasePlayer player = other.GetComponentInParent<BasePlayer>();
                if (player != null && player.userID.IsSteamId())
                {
                    ins.players.Remove(player);
                    if (ins._config.GUI.IsGUI) CuiHelper.DestroyUi(player, "TextMain");
                    if (ins._config.eventZone.isCreateZonePVP)
                    {
                        if (ins.plugins.Exists("DynamicPVP") && (bool)ins.DynamicPVP.Call("IsPlayerInPVPDelay", player.userID)) return;
                        NotifyManager.SendMessageToPlayer(player, "ExitPVP", ins._config.prefix);
                    }
                }
            }

            void MessageGUI(BasePlayer player, string text)
            {
                CuiHelper.DestroyUi(player, "TextMain");

                CuiElementContainer container = new CuiElementContainer();

                container.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0" },
                    RectTransform = { AnchorMin = ins._config.GUI.AnchorMin, AnchorMax = ins._config.GUI.AnchorMax },
                    CursorEnabled = false,
                }, "Hud", "TextMain");

                container.Add(new CuiElement
                {
                    Parent = "TextMain",
                    Components =
                {
                    new CuiTextComponent() { Color = "1 1 1 1", FadeIn = 0f, Text = text, FontSize = 24, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" },
                    new CuiOutlineComponent { Distance = "1 1", Color = "0 0 0 1" },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                }
                });

                CuiHelper.AddUi(player, container);
            }

            void OnDestroy()
            {
                if (guiCoroune != null) ServerMgr.Instance.StopCoroutine(guiCoroune);
                foreach (BaseEntity sphere in spheres) if (sphere != null && !sphere.IsDestroyed) sphere.Kill();
                foreach (BasePlayer player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, "TextMain");
            }
        }

        class RootCar : FacepunchBehaviour
        {
            internal static RootCar rootCar;
            internal BasicCar basicCar;
            internal List<Vector3> root = new List<Vector3>();
            BasePlayer player;

            internal static void RootStop()
            {
                if (rootCar != null && !rootCar.basicCar.IsDestroyed) rootCar.basicCar.Kill();
            }

            internal static void CreateRootCar(BasePlayer player)
            {
                if (rootCar != null)
                {
                    NotifyManager.SendMessageToPlayer(player, $"{ins._config.prefix} The route is <color=#738d43>already</color> being recorded!");
                    return;
                }
                NotifyManager.SendMessageToPlayer(player, $"{ins._config.prefix} To build a route, drive a car along it and write to the chat: <color=#738d43>convoyrootsave [rootgroupname]</color>\nTo reset the route, print to the chat: <color=#738d43>convoyrootstop</color>");
                BasicCar car = GameManager.server.CreateEntity("assets/content/vehicles/sedan_a/sedantest.entity.prefab", player.transform.position + new Vector3(0, 0.3f, 0), player.eyes.GetLookRotation()) as BasicCar;
                car.enableSaving = false;
                car.Spawn();
                rootCar = car.gameObject.AddComponent<RootCar>();
                rootCar.InitSedan(player);

                BaseVehicle.MountPointInfo mountPointInfo = car.mountPoints[0];
                player.MountObject(mountPointInfo.mountable);
                mountPointInfo.mountable.MountPlayer(player);
            }

            internal static void SaveRoot(BasePlayer player, string rootName)
            {
                if (rootCar.root.Count < 50) NotifyManager.SendMessageToPlayer(player, $"{ins._config.prefix} The route is too short!");
                else
                {
                    List<string> root = new List<string>();
                    foreach (Vector3 vector in rootCar.root) root.Add(vector.ToString());
                    if (!ins.roots.ContainsKey(rootName)) ins.roots.Add(rootName, new List<List<string>>());
                    ins.roots[rootName].Add(root);
                    ins.SaveData();
                    RootStop();
                    NotifyManager.SendMessageToPlayer(player, $"{ins._config.prefix} Route added to group <color=#738d43>{rootName}</color>");
                }
            }

            void InitSedan(BasePlayer player)
            {
                basicCar = GetComponent<BasicCar>();
                root.Add(basicCar.transform.position);
            }

            void FixedUpdate()
            {
                if (Vector3.Distance(basicCar.transform.position, root[root.Count - 1]) > 3) root.Add(basicCar.transform.position);
            }
        }

        static class EconomyManager
        {
            static readonly Dictionary<ulong, double> _playersBalance = new Dictionary<ulong, double>();

            internal static void ActionEconomy(ulong playerId, string type, string arg = "")
            {
                switch (type)
                {
                    case "Bradley":
                        AddBalance(playerId, ins._config.economyConfig.bradley);
                        break;
                    case "Npc":
                        AddBalance(playerId, ins._config.economyConfig.npc);
                        break;
                    case "LockedCrate":
                        AddBalance(playerId, ins._config.economyConfig.lockedCrate);
                        break;
                    case "Heli":
                        AddBalance(playerId, ins._config.economyConfig.heli);
                        break;
                    case "Sedan":
                        AddBalance(playerId, ins._config.economyConfig.sedan);
                        break;
                    case "Modular":
                        AddBalance(playerId, ins._config.economyConfig.modularCar);
                        break;
                }
            }

            static void AddBalance(ulong playerId, double balance)
            {
                if (balance == 0) return;
                if (_playersBalance.ContainsKey(playerId)) _playersBalance[playerId] += balance;
                else _playersBalance.Add(playerId, balance);
            }

            internal static void SendBalance()
            {
                if (!ins._config.economyConfig.enable || _playersBalance.Count == 0) return;
                foreach (KeyValuePair<ulong, double> dic in _playersBalance)
                {
                    if (dic.Value < ins._config.economyConfig.minEconomyPiont) continue;
                    int intCount = Convert.ToInt32(dic.Value);
                    if (ins._config.economyConfig.plugins.Contains("Economics") && ins.plugins.Exists("Economics") && dic.Value > 0) ins.Economics.Call("Deposit", dic.Key.ToString(), dic.Value);
                    if (ins._config.economyConfig.plugins.Contains("Server Rewards") && ins.plugins.Exists("ServerRewards") && intCount > 0) ins.ServerRewards.Call("AddPoints", dic.Key, intCount);
                    if (ins._config.economyConfig.plugins.Contains("IQEconomic") && ins.plugins.Exists("IQEconomic") && intCount > 0) ins.IQEconomic.Call("API_SET_BALANCE", dic.Key, intCount);
                    BasePlayer player = BasePlayer.FindByID(dic.Key);
                    if (player != null) NotifyManager.SendMessageToPlayer(player, "SendEconomy", ins._config.prefix, dic.Value);
                }

                double max = 0;
                ulong winnerId = 0;
                foreach (var a in _playersBalance)
                {
                    if (a.Value > max)
                    {
                        max = a.Value;
                        winnerId = a.Key;
                    }
                }
                DefineEventWinner();
                if (max >= ins._config.economyConfig.minCommandPoint) foreach (string command in ins._config.economyConfig.commands) ins.Server.Command(command.Replace("{steamid}", $"{winnerId}"));
                _playersBalance.Clear();
            }

            static void DefineEventWinner()
            {
                if (_playersBalance.Count == 0) return;
                float maxPoint = (float)_playersBalance.Max(x => (float)x.Value).Value;
                var winnerPair = _playersBalance.FirstOrDefault(x => x.Value == maxPoint);
                if (winnerPair.Value > 0) Interface.CallHook("OnConvoyEventWin", winnerPair.Key);
            }
        }

        static class NotifyManager
        {
            internal static void PrintError(BasePlayer player, string langKey, params object[] args)
            {
                if (player == null) ins.PrintError(ClearColorAndSize(GetMessage(langKey, null, args)));
                else ins.PrintToChat(player, GetMessage(langKey, player.UserIDString, args));
            }

            internal static string ClearColorAndSize(string message)
            {
                message = message.Replace("</color>", string.Empty);
                message = message.Replace("</size>", string.Empty);
                while (message.Contains("<color="))
                {
                    int index = message.IndexOf("<color=");
                    message = message.Remove(index, message.IndexOf(">", index) - index + 1);
                }
                while (message.Contains("<size="))
                {
                    int index = message.IndexOf("<size=");
                    message = message.Remove(index, message.IndexOf(">", index) - index + 1);
                }
                return message;
            }

            internal static void SendMessageToAll(string langKey, params object[] args)
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList) if (player != null) SendMessageToPlayer(player, langKey, args);
                SendDiscordMessage(langKey, args);
            }

            internal static void SendMessageToPlayer(BasePlayer player, string langKey, params object[] args)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] is int) args[i] = GetTimeMessage(player.UserIDString, (int)args[i]);
                }

                if (ins._config.IsChat) ins.PrintToChat(player, GetMessage(langKey, player.UserIDString, args));
                if (ins._config.GUIAnnouncements.isGUIAnnouncements) ins.GUIAnnouncements?.Call("CreateAnnouncement", ClearColorAndSize(GetMessage(langKey, player.UserIDString, args)), ins._config.GUIAnnouncements.bannerColor, ins._config.GUIAnnouncements.textColor, player, ins._config.GUIAnnouncements.apiAdjustVPosition);
                if (ins._config.Notify.IsNotify) player.SendConsoleCommand($"notify.show {ins._config.Notify.Type} {ClearColorAndSize(GetMessage(langKey, player.UserIDString, args))}");
            }

            internal static void SendDiscordMessage(string langKey, params object[] args)
            {
                if (CanSendDiscordMessage() && ins._config.discord.keys.Contains(langKey))
                {
                    for (int i = 0; i < args.Length; i++)
                    {
                        if (args[i] is int) args[i] = GetTimeMessage(null, (int)args[i]);
                    }

                    object fields = new[] { new { name = ins.Title, value = ClearColorAndSize(GetMessage(langKey, null, args)), inline = false } };
                    ins.DiscordMessages?.Call("API_SendFancyMessage", ins._config.discord.webhookUrl, "", ins._config.discord.embedColor, JsonConvert.SerializeObject(fields), null, ins);
                }
            }

            static bool CanSendDiscordMessage() => ins._config.discord.isDiscord && !string.IsNullOrEmpty(ins._config.discord.webhookUrl) && ins._config.discord.webhookUrl != "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

            internal static string GetTimeMessage(string userIDString, int seconds)
            {
                string message = "";

                TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
                if (timeSpan.Hours > 0) message += $" {timeSpan.Hours} {GetMessage("Hours", userIDString)}";
                if (timeSpan.Minutes > 0) message += $" {timeSpan.Minutes} {GetMessage("Minutes", userIDString)}";
                if (message == "") message += $" {timeSpan.Seconds} {GetMessage("Seconds", userIDString)}";

                return message;
            }
        }
        #endregion Classes 

        float GetSpeed(float step, float distance, float maxSpeed, float multiplicator)
        {
            float speed;
            float stopDistance = 10;
            if (distance <= stopDistance) return -1;
            speed = step * (distance - 5 - stopDistance) * multiplicator;
            if (speed < 0) return 0;
            if (speed > maxSpeed) return maxSpeed;
            return speed;
        }

        void GetGlobal(BaseEntity parrent, Vector3 localPosition, Vector3 localRotation, out Vector3 globalPosition, out Vector3 globalRotation)
        {
            globalPosition = parrent.transform.TransformPoint(localPosition);
            globalRotation = parrent.transform.rotation.eulerAngles + localRotation;
        }

        private JObject GetNpcConfig(bool driver, bool passenger, NpcConfig config)
        {
            bool passive = !driver || (passenger && _config.blockFirstAttack);

            HashSet<string> states = new HashSet<string> { "RoamState", "ChaseState", "CombatState" };
            if (config.beltItems.Any(x => x.shortName == "rocket.launcher" || x.shortName == "explosive.timed")) states.Add("RaidState");
            return new JObject
            {
                ["Name"] = config.name,
                ["WearItems"] = new JArray { config.wearItems.Select(x => new JObject { ["ShortName"] = x.shortName, ["SkinID"] = x.skinID }) },
                ["BeltItems"] = new JArray { config.beltItems.Select(x => new JObject { ["ShortName"] = x.shortName, ["Amount"] = x.amount, ["SkinID"] = x.skinID, ["Mods"] = new JArray { x.Mods.ToHashSet() }, ["Ammo"] = string.Empty }) },
                ["Kit"] = config.kit,
                ["Health"] = config.health,
                ["RoamRange"] = config.roamRange,
                ["ChaseRange"] = config.chaseRange,
                ["DamageScale"] = config.damageScale,
                ["TurretDamageScale"] = 1f,
                ["AimConeScale"] = config.aimConeScale,
                ["DisableRadio"] = config.disableRadio,
                ["CanUseWeaponMounted"] = true,
                ["CanRunAwayWater"] = true,
                ["Speed"] = config.speed,
                ["AreaMask"] = 1,
                ["AgentTypeID"] = -1372625422,
                ["HomePosition"] = string.Empty,
                ["States"] = new JArray { states },
                ["Sensory"] = new JObject
                {
                    ["AttackRangeMultiplier"] = config.attackRangeMultiplier,
                    ["SenseRange"] = config.senseRange,
                    ["MemoryDuration"] = config.memoryDuration,
                    ["CheckVisionCone"] = config.checkVisionCone,
                    ["VisionCone"] = config.visionCone
                }
            };
        }

        void ChechGarbage(Vector3 pos)
        {
            foreach (Collider collider in UnityEngine.Physics.OverlapSphere(pos, 10f))
            {
                BaseEntity entity = collider.ToBaseEntity();
                if (entity != null && !entity.IsDestroyed && (trashList.Contains(entity.ShortPrefabName) || barrels.Contains(entity.ShortPrefabName))) entity.Kill();
            }
        }

        HashSet<string> barrels = new HashSet<string>
        {
            "loot_barrel_1",
            "loot_barrel_2",
            "loot-barrel-2",
            "loot-barrel-1",
            "oil_barrel"
        };

        HashSet<string> trashList = new HashSet<string>
        {
            "minicopter.entity",
            "scraptransporthelicopter",
            "rowboat",
            "rhib",
            "3module_car_spawned.entity",
            "1module_passengers_armored",
            "hotairballoon",
            "wolf",
            "2module_car_spawned.entity",
            "chicken",
            "boar",
            "stag",
            "bear",
            "saddletest",
            "testridablehorse",
            "servergibs_bradley",
            "loot_barrel_1",
            "loot_barrel_2",
            "loot-barrel-2",
            "loot-barrel-1",
            "oil_barrel",
            "stone-ore",
            "sulfur-ore",
            "metal-ore",
        };

        HashSet<string> trains = new HashSet<string>
        {
            "trainwagona.entity",
            "trainwagonb.entity",
            "trainwagonc.entity",
            "trainwagond.entity",
            "workcart_aboveground.entity",
            "workcart_aboveground2.entity",
            "locomotive.entity",
            "trainwagonunloadable.entity",
            "trainwagonunloadablefuel.entity",
            "trainwagonunloadableloot.entity"
        };

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PreStart"] = "{0} Через <color=#738d43>{1}</color>. начнется перевозка груза по автодороге!",
                ["ConvoyAttacked"] = "{0} {1} <color=#ce3f27>напал</color> на конвой",
                ["EventStart"] = "{0} Конвой <color=#738d43>начал</color> движение!",
                ["SecurityKill"] = "{0} Охрана конвоя была <color=#738d43>успешно</color> уничтожена!",
                ["Failed"] = "{0} Грузовик с грузом <color=#ce3f27>уничтожен</color>! Добыча <color=#ce3f27>потеряна</color>!",
                ["StartHackCrate"] = "{0} {1} <color=#738d43>начал</color> взлом заблокированного ящика!",
                ["PreFinish"] = "{0} Ивент будет окончен через <color=#ce3f27>{1}</color>",
                ["Finish"] = "{0} Перевозка груза <color=#ce3f27>окончена</color>!",
                ["CantHackCrate"] = "{0} Для того чтобы открыть ящик убейте все <color=#ce3f27>сопровождающие</color> транспортные средства!",
                ["EventActive"] = "{0} Ивент в данный момент активен, сначала завершите текущий ивент (<color=#ce3f27>/convoystop</color>)!",
                ["EnterPVP"] = "{0} Вы <color=#ce3f27>вошли</color> в PVP зону, теперь другие игроки <color=#ce3f27>могут</color> наносить вам урон!",
                ["ExitPVP"] = "{0} Вы <color=#738d43>вышли</color> из PVP зоны, теперь другие игроки <color=#738d43>не могут</color> наносить вам урон!",
                ["GUI"] = "Груз будет уничтожен через <color=#ce3f27>{0}</color>",
                ["SendEconomy"] = "{0} Вы <color=#738d43>получили</color> <color=#55aaff>{1}</color> баллов в экономику за прохождение ивента",
                ["Hours"] = "ч.",
                ["Minutes"] = "м.",
                ["Seconds"] = "с.",
                ["Distance"] = "Подойдите <color=#ce3f27>ближе</color>!",
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PreStart"] = "{0} In <color=#738d43>{1}</color> the cargo will be transported along the road!",
                ["ConvoyAttacked"] = "{0} {1} <color=#ce3f27>attacked</color> a convoy",
                ["EventStart"] = "{0} The convoy <color=#738d43>started</color> moving",
                ["SecurityKill"] = "{0} The guard of the convoy was <color=#738d43>destroyed</color>!",
                ["Failed"] = "{0} The cargo truck has been <color=#ce3f27>destroyed</color>! The loot is <color=#ce3f27>lost</color>!",
                ["StartHackCrate"] = "{0} {1} started <color=#738d43>hacking</color> the locked crate!",
                ["PreFinish"] = "{0} The event will be over in <color=#ce3f27>{1}</color>",
                ["Finish"] = "{0} The event is <color=#ce3f27>over</color>!",
                ["CantHackCrate"] = "{0} To open the crate, kill all the <color=#ce3f27>accompanying</color> vehicles!",
                ["EventActive"] = "{0} This event is active now. To finish this event (<color=#ce3f27/convoystop</color>)!",
                ["EnterPVP"] = "{0} You <color=#ce3f27>have entered</color> the PVP zone, now other players <color=#ce3f27>can damage</color> you!",
                ["ExitPVP"] = "{0} You <color=#738d43>have gone out</color> the PVP zone, now other players <color=#738d43>can’t damage</color> you!",
                ["GUI"] = "The cargo will be destroyed in <color=#ce3f27>{0}</color>",
                ["SendEconomy"] = "{0} You <color=#738d43>have earned</color> <color=#55aaff>{1}</color> points in economics for participating in the event",
                ["Hours"] = "h.",
                ["Minutes"] = "m.",
                ["Seconds"] = "s.",
                ["Distance"] = "Come <color=#ce3f27>closer</color>!",
            }, this);
        }

        internal static string GetMessage(string langKey, string userID) => ins.lang.GetMessage(langKey, ins, userID);

        internal static string GetMessage(string langKey, string userID, params object[] args) => (args.Length == 0) ? GetMessage(langKey, userID) : string.Format(GetMessage(langKey, userID), args);
        #endregion Lang

        #region Data 

        Dictionary<string, List<List<string>>> roots = new Dictionary<string, List<List<string>>>();

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Title, roots);

        private void LoadData() => roots = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, List<List<string>>>>(Title);

        #endregion Data 

        #region Config  

        PluginConfig _config;

        protected override void LoadDefaultConfig() => _config = PluginConfig.DefaultConfig();

        public class ConvoySetting
        {
            [JsonProperty(en ? "Name" : "Название пресета")] public string name { get; set; }
            [JsonProperty(en ? "Name displayed on the map (For custom marker)" : "Отображаемое на карте название (для кастомного маркера)")] public string displayName { get; set; }
            [JsonProperty(en ? "Automatic startup" : "Автоматический запуск")] public bool on { get; set; }
            [JsonProperty(en ? "Probability of a preset [0.0-100.0]" : "Вероятность пресета [0.0-100.0]")] public float chance { get; set; }
            [JsonProperty(en ? "Enable the helicopter" : "Включить вертолет")] public bool heliOn { get; set; }
            [JsonProperty(en ? "The number of Bradleys ahead the truck" : "Количество бредли впереди грузовика")] public int firstBradleyCount { get; set; }
            [JsonProperty(en ? "Number of Modular cars ahead the truck" : "Количество модульных впереди позади грузовика")] public int firstModularCount { get; set; }
            [JsonProperty(en ? "Number of Sedans ahead the truck" : "Количество седанов впереди грузовика")] public int firstSedanCount { get; set; }
            [JsonProperty(en ? "Number of Sedans behind the truck" : "Количество седанов позади грузовика")] public int endSedanCount { get; set; }
            [JsonProperty(en ? "Number of Modular cars behind the truck" : "Количество модульных машин позади грузовика")] public int endModularCount { get; set; }
            [JsonProperty(en ? "The number of Bradleys behind the truck" : "Количество бредли позади грузовика")] public int endBradleyCount { get; set; }
            [JsonProperty(en ? "Bradley preset" : "Пресет бредли")] public string bradleyConfigurationName { get; set; }
            [JsonProperty(en ? "Sedan preset" : "Пресет седана")] public string sedanConfigurationName { get; set; }
            [JsonProperty(en ? "Truck preset" : "Пресет грузовика")] public string modularConfigurationName { get; set; }
            [JsonProperty(en ? "Modular preset" : "Пресет модульных машин")] public string supportodularConfigurationName { get; set; }
            [JsonProperty(en ? "Heli preset" : "Пресет вертолета")] public string heliConfigurationName { get; set; }
        }

        public class ModularConfig : SupportModularConfig
        {
            [JsonProperty(en ? "Time to unlock the crates [sec.]" : "Время до открытия заблокированного ящика [sec.]")] public float crateUnlockTime { get; set; }
            [JsonProperty(en ? "Location of the locked crate" : "Расположение заблокированного ящика")] public CoordConfig crateLocation { get; set; }
            [JsonProperty(en ? "Which loot table should the plugin use? (0 - default, BetterLoot, MagicLoot; 1 - own; 2 - AlphaLoot)" : "Какую таблицу лута необходимо использовать? (0 - стандартную, BetterLoot, MagicLoot; 1 - собственную; 2 - AlphaLoot)")] public int typeLootTable { get; set; }
            [JsonProperty(en ? "Own loot table" : "Собственная таблица лута")] public LootTableConfig lootTable { get; set; }
        }

        public class SupportModularConfig
        {
            [JsonProperty(en ? "Name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty(en ? "Prefab Name" : "Название префаба машины")] public string prefabName { get; set; }
            [JsonProperty(en ? "Scale damage" : "Множитель урона")] public float damageMultiplier { get; set; }
            [JsonProperty(en ? "Modules" : "Модули")] public List<string> modules { get; set; }
            [JsonProperty(en ? "NPC preset" : "Пресет НПС")] public string npcName { get; set; }
            [JsonProperty(en ? "Location of NPCs" : "Расположение NPC")] public List<CoordConfig> coordinates { get; set; }
        }

        public class SedanConfig
        {
            [JsonProperty(en ? "Name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty("HP")] public float hp { get; set; }
            [JsonProperty(en ? "NPC preset" : "Пресет НПС")] public string npcName { get; set; }
            [JsonProperty(en ? "Location of all NPCs" : "Расположение NPC")] public List<CoordConfig> coordinates { get; set; }
        }

        public class BradleyConfig
        {
            [JsonProperty(en ? "Name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty("HP")] public float hp { get; set; }
            [JsonProperty(en ? "Scale damage" : "Множитель урона")] public float scaleDamage { get; set; }
            [JsonProperty(en ? "The viewing distance" : "Дальность обзора")] public float viewDistance { get; set; }
            [JsonProperty(en ? "Radius of search" : "Радиус поиска")] public float searchDistance { get; set; }
            [JsonProperty(en ? "The multiplier of Machine-gun aim cone" : "Множитель разброса пулемёта")] public float coaxAimCone { get; set; }
            [JsonProperty(en ? "The multiplier of Machine-gun fire rate" : "Множитель скорострельности пулемёта")] public float coaxFireRate { get; set; }
            [JsonProperty(en ? "Amount of Machine-gun burst shots" : "Кол-во выстрелов очереди пулемёта")] public int coaxBurstLength { get; set; }
            [JsonProperty(en ? "The time between shots of the main gun [sec.]" : "Время между залпами основного орудия [sec.]")] public float nextFireTime { get; set; }
            [JsonProperty(en ? "The time between shots of the main gun in a fire rate [sec.]" : "Время между выстрелами основного орудия в залпе [sec.]")] public float topTurretFireRate { get; set; }
            [JsonProperty(en ? "Numbers of crates" : "Кол-во ящиков после уничтожения")] public int countCrates { get; set; }
            [JsonProperty(en ? "NPC preset" : "Пресет НПС")] public string npcName { get; set; }
            [JsonProperty(en ? "Location of all NPCs" : "Расположение NPC")] public List<CoordConfig> coordinates { get; set; }
            [JsonProperty(en ? "Open crates after spawn [true/false]" : "Открывать ящики сразу после спавна [true/false]")] public bool offDelay { get; set; }
            [JsonProperty(en ? "Which loot table should the plugin use? (0 - default, BetterLoot, MagicLoot; 1 - own; 2 - AlphaLoot)" : "Какую таблицу лута необходимо использовать? (0 - стандартную, BetterLoot, MagicLoot; 1 - собственную; 2 - AlphaLoot)")] public int typeLootTable { get; set; }
            [JsonProperty(en ? "Own loot table" : "Собственная таблица лута")] public LootTableConfig lootTable { get; set; }
        }

        public class HeliConfig
        {
            [JsonProperty(en ? "Name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty("HP")] public float hp { get; set; }
            [JsonProperty(en ? "HP of the main rotor" : "HP главного винта")] public float mainRotorHealth { get; set; }
            [JsonProperty(en ? "HP of tail rotor" : "HP хвостового винта")] public float rearRotorHealth { get; set; }
            [JsonProperty(en ? "Numbers of crates" : "Количество ящиков")] public int cratesAmount { get; set; }
            [JsonProperty(en ? "Flying height" : "Высота полета")] public float height { get; set; }
            [JsonProperty(en ? "Bullet speed" : "Скорость пуль")] public float bulletSpeed { get; set; }
            [JsonProperty(en ? "Bullet Damage" : "Урон пуль")] public float bulletDamage { get; set; }
            [JsonProperty(en ? "The distance to which the helicopter can move away from the convoy" : "Дистанция, на которую вертолет может отдаляться от конвоя")] public float distance { get; set; }
            [JsonProperty(en ? "Speed" : "Скорость")] public float speed { get; set; }
            [JsonProperty(en ? "Open crates after spawn [true/false]" : "Открывать ящики после спавна [true/false]")] public bool offDelay { get; set; }
            [JsonProperty(en ? "Which loot table should the plugin use? (0 - default, BetterLoot, MagicLoot; 1 - own; 2 - AlphaLoot)" : "Какую таблицу лута необходимо использовать? (0 - стандартную, BetterLoot, MagicLoot; 1 - собственную; 2 - AlphaLoot)")] public int typeLootTable { get; set; }
            [JsonProperty(en ? "Own loot table" : "Собственная таблица лута")] public LootTableConfig lootTable { get; set; }
        }

        public class NpcConfig
        {
            [JsonProperty(en ? "Name" : "Название")] public string name { get; set; }
            [JsonProperty(en ? "Health" : "Кол-во ХП")] public float health { get; set; }
            [JsonProperty(en ? "Should remove the corpse?" : "Удалять труп?")] public bool deleteCorpse { get; set; }
            [JsonProperty(en ? "Roam Range" : "Дальность патрулирования местности")] public float roamRange { get; set; }
            [JsonProperty(en ? "Chase Range" : "Дальность погони за целью")] public float chaseRange { get; set; }
            [JsonProperty(en ? "Attack Range Multiplier" : "Множитель радиуса атаки")] public float attackRangeMultiplier { get; set; }
            [JsonProperty(en ? "Sense Range" : "Радиус обнаружения цели")] public float senseRange { get; set; }
            [JsonProperty(en ? "Memory duration [sec.]" : "Длительность памяти цели [sec.]")] public float memoryDuration { get; set; }
            [JsonProperty(en ? "Scale damage" : "Множитель урона")] public float damageScale { get; set; }
            [JsonProperty(en ? "Aim Cone Scale" : "Множитель разброса")] public float aimConeScale { get; set; }
            [JsonProperty(en ? "Detect the target only in the NPC's viewing vision cone?" : "Обнаруживать цель только в углу обзора NPC? [true/false]")] public bool checkVisionCone { get; set; }
            [JsonProperty(en ? "Vision Cone" : "Угол обзора")] public float visionCone { get; set; }
            [JsonProperty(en ? "Speed" : "Скорость")] public float speed { get; set; }
            [JsonProperty(en ? "Wear items" : "Одежда")] public List<NpcWear> wearItems { get; set; }
            [JsonProperty(en ? "Belt items" : "Быстрые слоты")] public List<NpcBelt> beltItems { get; set; }
            [JsonProperty(en ? "Which loot table should the plugin use? (0 - default, BetterLoot, MagicLoot; 1 - own)" : "Какую таблицу лута необходимо использовать? (0 - стандартную, BetterLoot, MagicLoot; 1 - собственную)")] public int typeLootTable { get; set; }
            [JsonProperty(en ? "Kit" : "Kit")] public string kit { get; set; }
            [JsonProperty(en ? "Disable radio effects? [true/false]" : "Отключать эффекты рации? [true/false]")] public bool disableRadio { get; set; }
            [JsonProperty(en ? "Own loot table" : "Собственная таблица лута")] public LootTableConfig lootTable { get; set; }
        }

        public class CoordConfig
        {
            [JsonProperty(en ? "Position" : "Позиция")] public string position { get; set; }
            [JsonProperty(en ? "Rotation" : "Вращение")] public string rotation { get; set; }
        }

        public class NpcWear
        {
            [JsonProperty(en ? "ShortName" : "ShortName")] public string shortName { get; set; }
            [JsonProperty(en ? "skinID (0 - default)" : "SkinID (0 - default)")] public ulong skinID { get; set; }
        }

        public class NpcBelt
        {
            [JsonProperty(en ? "ShortName" : "ShortName")] public string shortName { get; set; }
            [JsonProperty(en ? "Amount" : "Кол-во")] public int amount { get; set; }
            [JsonProperty(en ? "skinID (0 - default)" : "SkinID (0 - default)")] public ulong skinID { get; set; }
            [JsonProperty(en ? "Mods" : "Модификации на оружие")] public List<string> Mods { get; set; }
        }

        public class ItemConfig
        {
            [JsonProperty("ShortName")] public string shortName { get; set; }
            [JsonProperty(en ? "Minimum" : "Минимальное кол-во")] public int minAmount { get; set; }
            [JsonProperty(en ? "Maximum" : "Максимальное кол-во")] public int maxAmount { get; set; }
            [JsonProperty(en ? "Chance [0.0-100.0]" : "Шанс выпадения предмета [0.0-100.0]")] public float chance { get; set; }
            [JsonProperty(en ? "Is this a blueprint? [true/false]" : "Это чертеж? [true/false]")] public bool isBluePrint { get; set; }
            [JsonProperty(en ? "SkinID (0 - default)" : "SkinID (0 - default)")] public ulong skinID { get; set; }
            [JsonProperty(en ? "Name (empty - default)" : "Название (empty - default)")] public string name { get; set; }
        }

        public class LootTableConfig
        {
            [JsonProperty(en ? "Minimum numbers of items" : "Минимальное кол-во элементов")] public int Min { get; set; }
            [JsonProperty(en ? "Maximum numbers of items" : "Максимальное кол-во элементов")] public int Max { get; set; }
            [JsonProperty(en ? "List of items" : "Список предметов")] public List<ItemConfig> Items { get; set; }
        }

        public class DomeConfig
        {
            [JsonProperty(en ? "Create a PVP zone in the convoy stop zone? (only for those who use the TruePVE plugin)[true/false]" : "Создавать зону PVP в зоне проведения ивента? (только для тех, кто использует плагин TruePVE) [true/false]")] public bool isCreateZonePVP { get; set; }
            [JsonProperty(en ? "Use the dome? [true/false]" : "Использовать ли купол? [true/false]")] public bool isDome { get; set; }
            [JsonProperty(en ? "Darkening the dome" : "Затемнение купола")] public int darkening { get; set; }
            [JsonProperty(en ? "Radius" : "Радиус")] public float radius { get; set; }
        }

        public class GUIConfig
        {
            [JsonProperty(en ? "Use the Countdown GUI? [true/false]" : "Использовать ли GUI обратного отсчета? [true/false]")] public bool IsGUI { get; set; }
            [JsonProperty("AnchorMin")] public string AnchorMin { get; set; }
            [JsonProperty("AnchorMax")] public string AnchorMax { get; set; }
        }

        public class ColorConfig
        {
            [JsonProperty("r")] public float r { get; set; }
            [JsonProperty("g")] public float g { get; set; }
            [JsonProperty("b")] public float b { get; set; }
        }

        public class MarkerConfig
        {
            [JsonProperty(en ? "Do you use the Marker? [true/false]" : "Использовать ли маркер? [true/false]")] public bool IsMarker { get; set; }
            [JsonProperty(en ? "Radius" : "Радиус")] public float Radius { get; set; }
            [JsonProperty(en ? "Alpha" : "Прозрачность")] public float Alpha { get; set; }
            [JsonProperty(en ? "Marker color" : "Цвет маркера")] public ColorConfig Color1 { get; set; }
            [JsonProperty(en ? "Outline color" : "Цвет контура")] public ColorConfig Color2 { get; set; }
        }

        public class GUIAnnouncementsConfig
        {
            [JsonProperty(en ? "Do you use the GUI Announcements? [true/false]" : "Использовать ли GUI Announcements? [true/false]")] public bool isGUIAnnouncements { get; set; }
            [JsonProperty(en ? "Banner color" : "Цвет баннера")] public string bannerColor { get; set; }
            [JsonProperty(en ? "Text color" : "Цвет текста")] public string textColor { get; set; }
            [JsonProperty(en ? "Adjust Vertical Position" : "Отступ от верхнего края")] public float apiAdjustVPosition { get; set; }
        }

        public class NotifyConfig
        {
            [JsonProperty(en ? "Do you use the Notify? [true/false]" : "Использовать ли Notify? [true/false]")] public bool IsNotify { get; set; }
            [JsonProperty(en ? "Type" : "Тип")] public string Type { get; set; }
        }

        public class DiscordConfig
        {
            [JsonProperty(en ? "Do you use the Discord? [true/false]" : "Использовать ли Discord? [true/false]")] public bool isDiscord { get; set; }
            [JsonProperty("Webhook URL")] public string webhookUrl;
            [JsonProperty(en ? "Embed Color (DECIMAL)" : "Цвет полосы (DECIMAL)")] public int embedColor { get; set; }
            [JsonProperty(en ? "Keys of required messages" : "Ключи необходимых сообщений")] public HashSet<string> keys { get; set; }
        }

        public class PveModeConfig
        {
            [JsonProperty(en ? "Use the PVE mode of the plugin? [true/false]" : "Использовать PVE режим работы плагина? [true/false]")] public bool pve { get; set; }
            [JsonProperty(en ? "The amount of damage that the player has to do to become the Event Owner" : "Кол-во урона, которое должен нанести игрок, чтобы стать владельцем ивента")] public float damage { get; set; }
            [JsonProperty(en ? "Damage coefficients for calculate to become the Event Owner" : "Коэффициенты урона для подсчета, чтобы стать владельцем события")] public HashSet<ScaleDamageConfig> scaleDamage { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event loot the crates? [true/false]" : "Может ли не владелец ивента грабить ящики? [true/false]")] public bool lootCrate { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event hack locked crates? [true/false]" : "Может ли не владелец ивента взламывать заблокированные ящики? [true/false]")] public bool hackCrate { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event loot NPC corpses? [true/false]" : "Может ли не владелец ивента грабить трупы NPC? [true/false]")] public bool lootNpc { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event deal damage to the NPC? [true/false]" : "Может ли не владелец ивента наносить урон по NPC? [true/false]")] public bool damageNpc { get; set; }
            [JsonProperty(en ? "Can an Npc attack a non-owner of the event? [true/false]" : "Может ли Npc атаковать не владельца ивента? [true/false]")] public bool targetNpc { get; set; }
            [JsonProperty(en ? "Can Bradley attack a non-owner of the event? [true/false]" : "Может ли Bradley атаковать не владельца ивента? [true/false]")] public bool targetTank { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event do damage to Bradley? [true/false]" : "Может ли не владелец ивента наносить урон по Bradley? [true/false]")] public bool damageTank { get; set   ; }
            [JsonProperty(en ? "Can Helicopter attack a non-owner of the event? [true/false]" : "Может ли Вертолет атаковать не владельца ивента? [true/false]")] public bool targetHeli { get; set; }
            [JsonProperty(en ? "Can the non-owner of the event do damage to Helicopter? [true/false]" : "Может ли не владелец ивента наносить урон по Вертолету? [true/false]")] public bool damageHeli { get; set; }
            [JsonProperty(en ? "Allow the non-owner of the event to enter the event zone? [true/false]" : "Разрешать входить внутрь зоны ивента не владельцу ивента? [true/false]")] public bool canEnter { get; set; }
            [JsonProperty(en ? "Allow a player who has an active cooldown of the Event Owner to enter the event zone? [true/false]" : "Разрешать входить внутрь зоны ивента игроку, у которого активен кулдаун на получение статуса владельца ивента? [true/false]")] public bool canEnterCooldownPlayer { get; set; }
            [JsonProperty(en ? "The time that the Event Owner may not be inside the event zone [sec.]" : "Время, которое владелец ивента может не находиться внутри зоны ивента [сек.]")] public int timeExitOwner { get; set; }
            [JsonProperty(en ? "The time until the end of Event Owner status when it is necessary to warn the player [sec.]" : "Время таймера до окончания действия статуса владельца ивента, когда необходимо предупредить игрока [сек.]")] public int alertTime { get; set; }
            [JsonProperty(en ? "Prevent the actions of the RestoreUponDeath plugin in the event zone? [true/false]" : "Запрещать работу плагина RestoreUponDeath в зоне действия ивента? [true/false]")] public bool restoreUponDeath { get; set; }
            [JsonProperty(en ? "The time that the player can`t become the Event Owner, after the end of the event and the player was its owner [sec.]" : "Время, которое игрок не сможет стать владельцем ивента, после того как ивент окончен и игрок был его владельцем [sec.]")] public double cooldownOwner { get; set; }
            [JsonProperty(en ? "Darkening the dome (0 - disables the dome)" : "Затемнение купола (0 - отключает купол)")] public int darkening { get; set; }
        }

        public class ScaleDamageConfig
        {
            [JsonProperty(en ? "Type of target" : "Тип цели")] public string Type { get; set; }
            [JsonProperty(en ? "Damage Multiplier" : "Множитель урона")] public float Scale { get; set; }
        }

        public class EconomyConfig
        {
            [JsonProperty(en ? "Enable economy" : "Включить экономику?")] public bool enable { get; set; }
            [JsonProperty(en ? "Which economy plugins do you want to use? (Economics, Server Rewards, IQEconomic)" : "Какие плагины экономики вы хотите использовать? (Economics, Server Rewards, IQEconomic)")] public HashSet<string> plugins { get; set; }
            [JsonProperty(en ? "The minimum value that a player must collect to get points for the economy" : "Минимальное значение, которое игрок должен заработать, чтобы получить баллы за экономику")] public double minEconomyPiont { get; set; }
            [JsonProperty(en ? "The minimum value that a winner must collect to make the commands work" : "Минимальное значение, которое победитель должен заработать, чтобы сработали команды")] public double minCommandPoint { get; set; }
            [JsonProperty(en ? "Killing an NPC" : "Убийство NPC")] public double npc { get; set; }
            [JsonProperty(en ? "Killing an Bradley" : "Уничтожение Bradley")] public double bradley { get; set; }
            [JsonProperty(en ? "Killing an Heli" : "Уничтожение вертолета")] public double heli { get; set; }
            [JsonProperty(en ? "Killing an sedan" : "Уничтожение седана")] public double sedan { get; set; }
            [JsonProperty(en ? "Killing an mpdular Car" : "Уничтожение модульной машины")] public double modularCar { get; set; }
            [JsonProperty(en ? "Hacking a locked crate" : "Взлом заблокированного ящика")] public double lockedCrate { get; set; }
            [JsonProperty(en ? "List of commands that are executed in the console at the end of the event ({steamid} - the player who collected the highest number of points)" : "Список команд, которые выполняются в консоли по окончанию ивента ({steamid} - игрок, который набрал наибольшее кол-во баллов)")] public HashSet<string> commands { get; set; }
        }

        public class BetterNpcConfig
        {
            [JsonProperty(en ? "Allow Npc spawn after destroying Bradley" : "Разрешить спавн Npc после уничтожения Бредли")] public bool bradleyNpc { get; set; }
            [JsonProperty(en ? "Allow Npc spawn after destroying Heli" : "Разрешить спавн Npc после уничтожения Вертолета")] public bool heliNpc { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(_config, true);
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        class PluginConfig
        {
            [JsonProperty(en ? "Version" : "Версия плагина", Order = 0)] public string version { get; set; }
            [JsonProperty(en ? "Prefix of chat messages" : "Префикс в чате", Order = 1)] public string prefix { get; set; }
            [JsonProperty(en ? "Enable automatic event holding [true/false]" : "Включить автоматическое проведение ивента [true/false]", Order = 2)] public bool autoEvent { get; set; }
            [JsonProperty(en ? "Minimum time between events [sec.]" : "Минимальное время между ивентами [sec.]", Order = 3)] public int minStartTime { get; set; }
            [JsonProperty(en ? "Maximum time between events [sec.]" : "Максимальное время между ивентами [sec.]", Order = 4)] public int maxStartTime { get; set; }
            [JsonProperty(en ? "Duration of the event [sec.]" : "Длительность ивента [sec.]", Order = 5)] public int eventTime { get; set; }
            [JsonProperty(en ? "Use a chat? [true/false]" : "Использовать ли чат? [true/false]", Order = 6)] public bool IsChat { get; set; }
            [JsonProperty(en ? "Time before the starting of the event after receiving a chat message [sec.]" : "Время до начала ивента после сообщения в чате [sec.]", Order = 7)] public int preStartTime { get; set; }
            [JsonProperty(en ? "Notification time until the end of the event [sec.] " : "Время оповещения до окончания ивента [sec.]", Order = 8)] public int preFinishTime { get; set; }
            [JsonProperty(en ? "If there is a ring road on the map, then the event will be held on it" : "Если на карте есть кольцевая дорога, то ивент будет проводиться на ней", Order = 9)] public bool rounRoadPriority { get; set; }
            [JsonProperty(en ? "The minimum length of the road on which the event can be held (Recommended values: standard map - 100, custom - 300)" : "Минимальное длина дороги, на которой может проводиться ивент (Рекомендуемые значения: стандартная карта - 100, кастомная - 300)", Order = 10)] public int roadCount { get; set; }
            [JsonProperty(en ? "The distance between the machines during spawn (Recommended values: standard map - 3, custom - 10)" : "Расстояние между машинами при спавне (Рекомендуемые значения: стандартная карта - 3, кастомная - 10)", Order = 11)] public int carDistance { get; set; }
            [JsonProperty(en ? "Custom route name" : "Пресет кастомного маршрута", Order = 12)] public string customRootName { get; set; }
            [JsonProperty(en ? "The time for which the convoy stops moving after receiving damage [sec.]" : "Время, на которое останавливается конвой, после получения урона [sec.]", Order = 13)] public int damamageStopTime { get; set; }
            [JsonProperty(en ? "Maximum distance for dealing damage to the convoy" : "Максимальное расстояние для нанесения урона конвою", Order = 14)] public float maxDamageDistance { get; set; }
            [JsonProperty(en ? "The convoy will not attack first [true/false]" : "Бредли и вертолет не будут атаковать первыми [true/false]", Order = 15)] public bool blockFirstAttack { get; set; }
            [JsonProperty(en ? "If an NPC has been killed, it will not spawn at the next stop of the convoy [true/false]" : "Если NPC был убит, то он не будет поялвляться при следующей остановке конвоя [true/false]", Order = 16)] public bool blockSpawnDieNpc { get; set; }
            [JsonProperty(en ? "It is necessary to stop the convoy to open the crate" : "Необходимо остановить конвой, чтобы открыть ящик", Order = 17)] public bool needStopConvoy { get; set; }
            [JsonProperty(en ? "It is necessary to kill all vehicles to open the crate" : "Необходимо убить все машины, чтобы открыть ящик", Order = 18)] public bool needKillCars { get; set; }
            [JsonProperty(en ? "It is necessary to kill all NPC to open the crate" : "Необходимо убить всех NPC, чтобы открыть ящик", Order = 19)] public bool needKillNpc { get; set; }
            [JsonProperty(en ? "Remove obstacles in front of the convoy [true/false]" : "Удалять преграды перед конвоем [true/false]", Order = 20)] public bool deleteBarriers { get; set; }
            [JsonProperty(en ? "Remove trees in front of the convoy (If the previous one is enabled) [true/false]" : "Удалять деревья перед конвоем (Если предыдущий включен) [true/false]", Order = 21)] public bool deleteTrees { get; set; }
            [JsonProperty(en ? "List of obstacles" : "Список преград", Order = 22)] public List<string> barriers { get; set; }
            [JsonProperty(en ? "Blocked roads (command /convoyroadblock)" : "Заблокированные дороги (команда /convoyroadblock)", Order = 23)] public List<int> blockRoads { get; set; }
            [JsonProperty(en ? "Convoy Presets" : "Пресеты конвоя", Order = 24)] public List<ConvoySetting> convoys { get; set; }
            [JsonProperty(en ? "Marker Setting" : "Настройки маркера", Order = 25)] public MarkerConfig marker { get; set; }
            [JsonProperty(en ? "Event zone" : "Настройка зоны ивента", Order = 26)] public DomeConfig eventZone { get; set; }
            [JsonProperty("GUI", Order = 27)] public GUIConfig GUI { get; set; }
            [JsonProperty(en ? "Bradley Configurations" : "Кофигурации бредли", Order = 28)] public List<BradleyConfig> bradleyConfiguration { get; set; }
            [JsonProperty(en ? "Sedan Configurations" : "Кофигурации седанов", Order = 29)] public List<SedanConfig> sedanConfiguration { get; set; }
            [JsonProperty(en ? "Truck Configurations" : "Кофигурации грузовиков", Order = 30)] public List<ModularConfig> modularConfiguration { get; set; }
            [JsonProperty(en ? "Modular Configurations" : "Кофигурации модульных машин", Order = 31)] public List<SupportModularConfig> supportModularConfiguration { get; set; }
            [JsonProperty(en ? "Heli Configurations" : "Кофигурации вертолетов", Order = 32)] public List<HeliConfig> heliesConfiguration { get; set; }
            [JsonProperty(en ? "NPC Configurations" : "Кофигурации NPC", Order = 33)] public List<NpcConfig> NPC { get; set; }
            [JsonProperty(en ? "Discord setting (only for DiscordMessages)" : "Настройка оповещений в Discord (только для DiscordMessages)", Order = 100)] public DiscordConfig discord { get; set; }
            [JsonProperty(en ? "Notify setting" : "Настройка Notify", Order = 101)] public NotifyConfig Notify { get; set; }
            [JsonProperty(en ? "GUI Announcements setting" : "Настройка GUI Announcements", Order = 102)] public GUIAnnouncementsConfig GUIAnnouncements { get; set; }
            [JsonProperty(en ? "BetterNpc Setting" : "Настройка плагина BetterNpc", Order = 103)] public BetterNpcConfig betterNpcConfig { get; set; }
            [JsonProperty(en ? "Setting Up the economy" : "Настройка экономики", Order = 104)] public EconomyConfig economyConfig { get; set; }
            [JsonProperty(en ? "PVE Mode Setting (only for users PveMode plugin)" : "Настройка PVE режима работы плагина (только для тех, кто использует плагин PveMode)", Order = 105)] public PveModeConfig pveMode { get; set; }

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    version = "2.2.0",
                    prefix = "[Convoy]",
                    autoEvent = true,
                    minStartTime = 3600,
                    maxStartTime = 3600,
                    eventTime = 1700,
                    IsChat = true,
                    preStartTime = 300,
                    preFinishTime = 900,
                    rounRoadPriority = true,
                    roadCount = 100,
                    carDistance = 3,
                    customRootName = "",
                    damamageStopTime = 180,
                    maxDamageDistance = 100f,
                    blockFirstAttack = false,
                    blockSpawnDieNpc = false,
                    needStopConvoy = true,
                    needKillCars = true,
                    needKillNpc = false,
                    deleteBarriers = true,
                    deleteTrees = true,
                    barriers = new List<string>
                    {
                        "minicopter.entity",
                        "scraptransporthelicopter",
                        "rowboat",
                        "rhib",
                        "1module_passengers_armored",
                        "2module_car_spawned.entity",
                        "3module_car_spawned.entity",
                        "4module_car_spawned.entity",
                        "hotairballoon",
                        "saddletest",
                        "testridablehorse",
                        "servergibs_bradley",
                        "loot_barrel_1",
                        "loot_barrel_2",
                        "loot-barrel-2",
                        "loot-barrel-1",
                        "oil_barrel",
                        "snowmobile",
                        "tomahasnowmobile",
                        "trainwagona.entity",
                        "trainwagonb.entity",
                        "trainwagonc.entity",
                        "trainwagond.entity",
                        "workcart_aboveground.entity",
                        "workcart_aboveground2.entity",
                        "locomotive.entity",
                        "trainwagonunloadable.entity",
                        "trainwagonunloadablefuel.entity",
                        "trainwagonunloadableloot.entity",
                        "xmasportalentry",
                        "stone-ore",
                        "sulfur-ore",
                        "metal-ore",
                    },
                    blockRoads = new List<int>(),
                    convoys = new List<ConvoySetting>
                    {
                        new ConvoySetting
                        {
                            name = "standart",
                            displayName = "Convoy",
                            chance = 75,
                            on = true,
                            firstBradleyCount = 1,
                            firstModularCount = 0,
                            firstSedanCount = 1,
                            endSedanCount = 1,
                            endModularCount = 0,
                            endBradleyCount = 1,
                            bradleyConfigurationName = "bradley_1",
                            modularConfigurationName = "truck_1",
                            sedanConfigurationName = "sedan_1",
                            supportodularConfigurationName = "modular_1",
                            heliOn = false,
                            heliConfigurationName = "heli_1"
                        },
                        new ConvoySetting
                        {
                            name = "hard",
                            displayName = "Hard convoy",
                            chance = 25,
                            on = true,
                            firstBradleyCount = 1,
                            firstModularCount = 1,
                            firstSedanCount = 1,
                            endSedanCount = 1,
                            endModularCount = 1,
                            endBradleyCount = 1,
                            bradleyConfigurationName = "bradley_1",
                            modularConfigurationName = "truck_1",
                            sedanConfigurationName = "sedan_1",
                            supportodularConfigurationName = "modular_1",
                            heliOn = true,
                            heliConfigurationName = "heli_1"
                        },
                    },
                    marker = new MarkerConfig
                    {
                        IsMarker = true,
                        Radius = 0.2f,
                        Alpha = 0.6f,
                        Color1 = new ColorConfig { r = 0.81f, g = 0.25f, b = 0.15f },
                        Color2 = new ColorConfig { r = 0f, g = 0f, b = 0f }
                    },
                    eventZone = new DomeConfig
                    {
                        isCreateZonePVP = false,
                        isDome = false,
                        darkening = 5,
                        radius = 70f
                    },
                    GUI = new GUIConfig
                    {
                        IsGUI = true,
                        AnchorMin = "0 0.9",
                        AnchorMax = "1 0.95"
                    },
                    bradleyConfiguration = new List<BradleyConfig>
                    {
                        new BradleyConfig
                        {
                            presetName = "bradley_1",
                            hp = 1000f,
                            scaleDamage = 1f,
                            viewDistance = 100.0f,
                            searchDistance = 100.0f,
                            coaxAimCone = 1.1f,
                            coaxFireRate = 1.0f,
                            coaxBurstLength = 10,
                            nextFireTime = 10f,
                            topTurretFireRate = 0.25f,
                            countCrates = 3,
                            npcName = "Tankman",
                            coordinates = new List<CoordConfig>
                            {
                                new CoordConfig { position = "(3, 0, 3)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(-3, 0, 3)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(3, 0, -3)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(3, 0, 0)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(-3, 0, 0)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(-3, 0, -3)", rotation = "(0, 0, 0)" }
                            },
                            offDelay = false,
                            typeLootTable = 0,
                            lootTable = new LootTableConfig
                            {
                                Min = 1,
                                Max = 2,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig
                                    {
                                        shortName = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 50.0f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    }
                                }
                            }
                        }
                    },
                    sedanConfiguration = new List<SedanConfig>
                    {
                        new SedanConfig
                        {
                            presetName = "sedan_1",
                            hp = 500f,
                            npcName = "ConvoyNPC",
                            coordinates = new List<CoordConfig>
                            {
                                new CoordConfig { position = "(2, 0, 2)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(-2, 0, 2)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(2, 0, -2)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(-2, 0, -2)", rotation = "(0, 0, 0)" }
                            },
                        }
                    },
                    modularConfiguration = new List<ModularConfig>
                    {
                        new ModularConfig
                        {
                            presetName = "truck_1",
                            prefabName = "assets/content/vehicles/modularcar/4module_car_spawned.entity.prefab",
                            damageMultiplier = 0.5f,
                            modules = new List<string> { "vehicle.1mod.engine", "vehicle.1mod.cockpit.armored", "vehicle.1mod.passengers.armored", "vehicle.1mod.flatbed" },
                            npcName = "ConvoyNPC",
                            coordinates = new List<CoordConfig>
                            {
                                new CoordConfig { position = "(2, 0, 2)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(-2, 0, 2)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(2, 0, -2)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(-2, 0, -2)", rotation = "(0, 0, 0)" }
                            },
                            crateLocation = new CoordConfig { position = "(0, 0.65, -2.35)", rotation = "(0, 180, 0)" },
                            crateUnlockTime = 10,
                            typeLootTable = 0,
                            lootTable = new LootTableConfig
                            {
                                Min = 1,
                                Max = 2,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig
                                    {
                                        shortName = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 50.0f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    }
                                }
                            }
                        }
                    },
                    supportModularConfiguration = new List<SupportModularConfig>
                    {
                        new SupportModularConfig
                        {
                            presetName = "modular_1",
                            prefabName = "assets/content/vehicles/modularcar/4module_car_spawned.entity.prefab",
                            damageMultiplier = 1f,
                            modules = new List<string> { "vehicle.1mod.engine", "vehicle.1mod.cockpit.armored", "vehicle.1mod.passengers.armored", "vehicle.1mod.passengers.armored" },
                            npcName = "ConvoyNPC",
                            coordinates = new List<CoordConfig>
                            {
                                new CoordConfig { position = "(2, 0, 2)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(-2, 0, 2)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(2, 0, -2)", rotation = "(0, 0, 0)" },
                                new CoordConfig { position = "(-2, 0, -2)", rotation = "(0, 0, 0)" }
                            }
                        }
                    },
                    heliesConfiguration = new List<HeliConfig>
                    {
                        new HeliConfig
                        {
                            presetName = "heli_1",
                            hp = 10000f,
                            cratesAmount = 3,
                            mainRotorHealth = 750f,
                            rearRotorHealth = 375f,
                            height = 50f,
                            bulletDamage = 20f,
                            bulletSpeed = 250f,
                            distance = 350f,
                            speed = 25f,
                            offDelay = false,
                            typeLootTable = 0,
                            lootTable = new LootTableConfig
                            {
                                Min = 1,
                                Max = 2,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig
                                    {
                                        shortName = "scrap",
                                        minAmount = 100,
                                        maxAmount = 200,
                                        chance = 50.0f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    }
                                }
                            }
                        }
                    },
                    NPC = new List<NpcConfig>
                    {
                        new NpcConfig
                        {
                            name = "ConvoyNPC",
                            health = 200f,
                            deleteCorpse = true,
                            wearItems = new List<NpcWear>
                            {
                                new NpcWear
                                {
                                    shortName = "metal.plate.torso",
                                    skinID = 1988476232
                                },
                                new NpcWear
                                {
                                    shortName = "riot.helmet",
                                    skinID = 1988478091
                                },
                                new NpcWear
                                {
                                    shortName = "pants",
                                    skinID = 1582399729
                                },
                                new NpcWear
                                {
                                    shortName = "tshirt",
                                    skinID = 1582403431
                                },
                                new NpcWear
                                {
                                    shortName = "shoes.boots",
                                    skinID = 0
                                }
                            },
                            beltItems = new List<NpcBelt>
                            {
                                new NpcBelt
                                {
                                    shortName = "rifle.lr300",
                                    amount = 1,
                                    skinID = 0,
                                    Mods = new List<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }
                                },
                                new NpcBelt
                                {
                                    shortName = "syringe.medical",
                                    amount = 10,
                                    skinID = 0,
                                    Mods = new List<string>()
                                },
                                new NpcBelt
                                {
                                    shortName = "grenade.f1",
                                    amount = 10,
                                    skinID = 0,
                                    Mods = new List<string>()
                                }
                            },
                            kit = "",
                            disableRadio = false,
                            roamRange = 5f,
                            chaseRange = 110f,
                            attackRangeMultiplier = 1f,
                            senseRange = 60f,
                            memoryDuration = 60f,
                            damageScale = 1f,
                            aimConeScale = 1f,
                            checkVisionCone = false,
                            visionCone = 135f,
                            speed = 7.5f,
                            lootTable = new LootTableConfig
                            {
                                Min = 2,
                                Max = 4,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig
                                    {
                                        shortName = "rifle.semiauto",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.09f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "shotgun.pump",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.09f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "pistol.semiauto",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.09f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "largemedkit",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.1f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "smg.2",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.1f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "pistol.python",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.1f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "smg.thompson",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.1f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "shotgun.waterpipe",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.2f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "shotgun.double",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.2f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "ammo.rifle.explosive",
                                        minAmount = 8,
                                        maxAmount = 8,
                                        chance = 0.2f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "pistol.revolver",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.2f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "ammo.rifle.hv",
                                        minAmount = 10,
                                        maxAmount = 10,
                                        chance = 0.2f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "ammo.rifle.incendiary",
                                        minAmount = 8,
                                        maxAmount = 8,
                                        chance = 0.5f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "ammo.pistol.hv",
                                        minAmount = 10,
                                        maxAmount = 10,
                                        chance = 0.5f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "ammo.pistol.fire",
                                        minAmount = 10,
                                        maxAmount = 10,
                                        chance = 1f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "ammo.shotgun.slug",
                                        minAmount = 4,
                                        maxAmount = 8,
                                        chance = 5f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "ammo.shotgun.fire",
                                        minAmount = 4,
                                        maxAmount = 14,
                                        chance = 5f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "ammo.shotgun",
                                        minAmount = 6,
                                        maxAmount = 12,
                                        chance = 8f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "bandage",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 17f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "syringe.medical",
                                        minAmount = 1,
                                        maxAmount = 2,
                                        chance = 34f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "ammo.rifle",
                                        minAmount = 12,
                                        maxAmount = 36,
                                        chance = 51f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "ammo.pistol",
                                        minAmount = 15,
                                        maxAmount = 45,
                                        chance = 52f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    }
                                }
                            }
                        },
                        new NpcConfig
                        {
                            name = "Tankman",
                            health = 500f,
                            deleteCorpse = true,
                            wearItems = new List<NpcWear>
                            {
                                new NpcWear
                                {
                                    shortName = "metal.plate.torso",
                                    skinID = 1988476232
                                },
                                new NpcWear
                                {
                                    shortName = "riot.helmet",
                                    skinID = 1988478091
                                },
                                new NpcWear
                                {
                                    shortName = "pants",
                                    skinID = 1582399729
                                },
                                new NpcWear
                                {
                                    shortName = "tshirt",
                                    skinID = 1582403431
                                },
                                new NpcWear
                                {
                                    shortName = "shoes.boots",
                                    skinID = 0
                                }
                            },
                            beltItems = new List<NpcBelt>
                            {
                                new NpcBelt
                                {
                                    shortName = "rifle.lr300",
                                    amount = 1,
                                    skinID = 0,
                                    Mods = new List<string> { "weapon.mod.flashlight", "weapon.mod.holosight" }
                                },
                                new NpcBelt
                                {
                                    shortName = "syringe.medical",
                                    amount = 10,
                                    skinID = 0,
                                    Mods = new List<string>()
                                },
                                new NpcBelt
                                {
                                    shortName = "rocket.launcher",
                                    amount = 1,
                                    skinID = 0,
                                    Mods = new List<string>()
                                }
                            },
                            kit = "",
                            disableRadio = false,
                            roamRange = 5f,
                            chaseRange = 110f,
                            attackRangeMultiplier = 1f,
                            senseRange = 60f,
                            memoryDuration = 60f,
                            damageScale = 1f,
                            aimConeScale = 1f,
                            checkVisionCone = false,
                            visionCone = 135f,
                            speed = 7.5f,
                            lootTable = new LootTableConfig
                            {
                                Min = 2,
                                Max = 4,
                                Items = new List<ItemConfig>
                                {
                                    new ItemConfig
                                    {
                                        shortName = "rifle.semiauto",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.09f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "shotgun.pump",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.09f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "pistol.semiauto",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.09f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "largemedkit",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.1f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "smg.2",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.1f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "pistol.python",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.1f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "smg.thompson",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.1f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "shotgun.waterpipe",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.2f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "shotgun.double",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.2f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "ammo.rifle.explosive",
                                        minAmount = 8,
                                        maxAmount = 8,
                                        chance = 0.2f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "pistol.revolver",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 0.2f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "ammo.rifle.hv",
                                        minAmount = 10,
                                        maxAmount = 10,
                                        chance = 0.2f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "ammo.rifle.incendiary",
                                        minAmount = 8,
                                        maxAmount = 8,
                                        chance = 0.5f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "ammo.pistol.hv",
                                        minAmount = 10,
                                        maxAmount = 10,
                                        chance = 0.5f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "ammo.pistol.fire",
                                        minAmount = 10,
                                        maxAmount = 10,
                                        chance = 1f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "ammo.shotgun.slug",
                                        minAmount = 4,
                                        maxAmount = 8,
                                        chance = 5f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "ammo.shotgun.fire",
                                        minAmount = 4,
                                        maxAmount = 14,
                                        chance = 5f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "ammo.shotgun",
                                        minAmount = 6,
                                        maxAmount = 12,
                                        chance = 8f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "bandage",
                                        minAmount = 1,
                                        maxAmount = 1,
                                        chance = 17f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "syringe.medical",
                                        minAmount = 1,
                                        maxAmount = 2,
                                        chance = 34f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "ammo.rifle",
                                        minAmount = 12,
                                        maxAmount = 36,
                                        chance = 51f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    },
                                    new ItemConfig
                                    {
                                        shortName = "ammo.pistol",
                                        minAmount = 15,
                                        maxAmount = 45,
                                        chance = 52f,
                                        isBluePrint = false,
                                        skinID = 0,
                                        name = ""
                                    }
                                }
                            }
                        }
                    },
                    discord = new DiscordConfig
                    {
                        isDiscord = false,
                        webhookUrl = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
                        embedColor = 13516583,
                        keys = new HashSet<string>
                        {
                            "PreStart",
                            "EventStart",
                            "PreFinish",
                            "Finish",
                            "StartHackCrate"
                        }
                    },
                    Notify = new NotifyConfig
                    {
                        IsNotify = false,
                        Type = "0"
                    },
                    GUIAnnouncements = new GUIAnnouncementsConfig
                    {
                        isGUIAnnouncements = false,
                        bannerColor = "Grey",
                        textColor = "White",
                        apiAdjustVPosition = 0.03f
                    },
                    betterNpcConfig = new BetterNpcConfig
                    {
                        bradleyNpc = false,
                        heliNpc = false
                    },
                    economyConfig = new EconomyConfig
                    {
                        enable = false,
                        plugins = new HashSet<string> { "Economics", "Server Rewards", "IQEconomic" },
                        minCommandPoint = 0,
                        minEconomyPiont = 0,
                        npc = 0.3,
                        bradley = 1,
                        heli = 1,
                        sedan = 0.3,
                        modularCar = 0.3,
                        lockedCrate = 0.5,
                        commands = new HashSet<string>()
                    },
                    pveMode = new PveModeConfig
                    {
                        pve = false,
                        damage = 500f,
                        scaleDamage = new HashSet<ScaleDamageConfig>
                        {
                            new ScaleDamageConfig { Type = "NPC", Scale = 1f },
                            new ScaleDamageConfig { Type = "Bradley", Scale = 1f },
                            new ScaleDamageConfig { Type = "Helicopter",  Scale = 1 }
                        },
                        lootCrate = false,
                        hackCrate = false,
                        lootNpc = false,
                        damageNpc = false,
                        targetNpc = false,
                        damageHeli = false,
                        targetHeli = false,
                        damageTank = false,
                        targetTank = false,
                        canEnter = false,
                        canEnterCooldownPlayer = true,
                        timeExitOwner = 300,
                        alertTime = 60,
                        restoreUponDeath = true,
                        cooldownOwner = 86400,
                        darkening = 5
                    },
                };
            }
        }
        #endregion Config
    }
}

namespace Oxide.Plugins.ConvoyExtensionMethods
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

        public static HashSet<TResult> Select<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> predicate)
        {
            HashSet<TResult> result = new HashSet<TResult>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(predicate(enumerator.Current));
            return result;
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

        public static HashSet<T> OfType<T>(this IEnumerable<BaseNetworkable> source)
        {
            HashSet<T> result = new HashSet<T>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (enumerator.Current is T) result.Add((T)(object)enumerator.Current);
            return result;
        }

        public static bool IsExists(this BaseNetworkable entity) => entity != null && !entity.IsDestroyed;

        public static void ClearItemsContainer(this ItemContainer container)
        {
            for (int i = container.itemList.Count - 1; i >= 0; i--)
            {
                Item item = container.itemList[i];
                item.RemoveFromContainer();
                item.Remove();
            }
        }

        public static bool IsRealPlayer(this BasePlayer player) => player != null && player.userID.IsSteamId();

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

        public static List<TSource> ToList<TSource>(this IEnumerable<TSource> source)
        {
            List<TSource> result = new List<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }

        public static HashSet<TSource> ToHashSet<TSource>(this IEnumerable<TSource> source)
        {
            HashSet<TSource> result = new HashSet<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current);
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

        public static TSource First<TSource>(this IList<TSource> source) => source[0];

        public static TSource Last<TSource>(this IList<TSource> source) => source[source.Count - 1];
    }
}