using CompanionServer.Handlers;
using Facepunch;
using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Plugins.BargesExtensionMethods;
using Rust;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using static StabilityEntity;

namespace Oxide.Plugins
{
    [Info("Barges", "Adem", "1.0.6")]

    class Barges : RustPlugin
    {
        #region Variables
        const bool en = true;
        static Barges ins;
        [PluginReference] Plugin GUIAnnouncements, Notify, DiscordMessages;
        HashSet<string> subscribeMethods = new HashSet<string>
        {
            "OnButtonPress",
            "CanBuild",
            "CanPickupEntity",
            "CanChangeGrade",
            "OnStructureRotate",
            "OnStructureUpgraded",
            "OnConstructionPlace",
            "OnPoweredLightsPointAdd",
            "OnEntityStabilityCheck",
            "OnEntityKill",
            "OnEntitySpawned",
            "CanMountEntity",
            "OnEntityMounted",
            "OnHammerHit",
            "CanPickupEntity",
            "OnWireClear",
            "OnWireConnect",
            "OnExplosiveThrown",
            "OnEntityTakeDamage",
            "CanLootEntity",
            "CanEntityTakeDamage"
        };
        #endregion Variables

        #region Hooks
        void Init()
        {
            Unsubscribes();
        }

        void OnServerInitialized()
        {
            ins = this;
            UpdateConfig();

            if (!TryLoadData())
            {
                NotifyManager.PrintError(null, "DataNotFound_Exeption");
                NextTick(() => Interface.Oxide.UnloadPlugin(Name));
                return;
            }

            LoadDefaultMessages();
            Subscribes();
            BoatConnectionTrigger.UpdateAllBoats();
            FishingVilageZoneController.CacheVilages();
            CargoShipManager.UpdateAllCargos();
            Barge.LoadBarges();
            BargeSpawner.StartPeriodicSpawn();
        }

        void Unload()
        {
            if (ins == null)
                return;

            Barge.SaveBarges(true);
            Barge.UnloadBarges();
            BargeSpawner.StopSpawning();
            BoatConnectionTrigger.Unload();
            FishingVilageZoneController.ClearZones();
            CargoShipManager.Unload();
            AdaptorInstallController.OnPluginUnload();
            ins = null;
        }

        void OnServerSave()
        {
            Barge.SaveBarges();
        }

        void OnButtonPress(PressButton button, BasePlayer player)
        {
            if (player == null || button == null)
                return;

            Barge barge = Barge.GetBargeByEntity(button);

            if (barge == null)
                return;

            BaseEntity parentEntity = button.GetParentEntity();

            if (parentEntity == null)
                return;

            BaseModule baseModule = parentEntity.GetComponentInChildren<BaseModule>();

            if (baseModule == null || !barge.IsPlayerCanInterract(player, true))
                return;

            baseModule.OnButtonPressed(player);
        }

        object OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null)
                return null;

            string checkClommand = command;

            foreach (string arg in args)
                checkClommand += $" {arg}";

            if (_config.mainConfig.blockedCommands.Any(x => x.ToLower() == checkClommand.ToLower()))
            {
                Barge barge = Barge.GetBargeByCollider(player);
                if (barge == null)
                    return null;

                NotifyManager.SendMessageToPlayer(player, "BlockedOnBarge");
                return true;
            }

            return null;
        }

        object CanBuild(Planner planner, Construction prefab, Construction.Target constructionTarget)
        {
            if (planner == null || constructionTarget.entity == null)
                return null;

            BasePlayer player = planner.GetOwnerPlayer();
            if (player == null)
                return null;

            BaseEntity parentEntity = constructionTarget.entity.GetParentEntity();
            if (parentEntity == null || parentEntity.net == null)
                return null;

            Barge barge = Barge.GetBargeByParentEntityNetId(parentEntity.net.ID.Value);
            if (barge == null)
            {
                if (Barge.GetBargeByEntity(parentEntity))
                    return true;

                return null;
            }

            Item item = planner.GetItem();
            if (item != null && item.info != null && _config.mainConfig.bloskedItemShortnames.Contains(item.info.shortname))
            {
                NotifyManager.SendMessageToPlayer(player, "BlockedOnBarge");
                return true;
            }

            if (!barge.IsPlayerCanInterract(player, true))
                return true;

            if (planner.ShortPrefabName.Contains("wallpaper") || (prefab != null && prefab.deployable != null && (prefab.deployable.fullName.Contains("frankensteintable"))))
            {
                //|| prefab.deployable.fullName.Contains("electrical.modularcarlift.deployed")

                NotifyManager.SendMessageToPlayer(player, "BlockedOnBarge");
                return true;
            }

            if (!barge.IsPlayerCanBuild(player, true))
                return true;

            return null;
        }

        object CanPickupEntity(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null)
                return null;

            Barge barge = Barge.GetBargeByEntity(entity);
            if (barge == null)
            {
                BuildingPrivlidge buildingPrivlidge = player.GetBuildingPrivilege();

                if (buildingPrivlidge != null && Barge.GetBargeByEntity(buildingPrivlidge) != null)
                {
                    BuildingPrivlidge entityBuildingPrivlidge = entity.GetBuildingPrivilege();

                    if (entityBuildingPrivlidge != null && !entityBuildingPrivlidge.IsAuthed(player))
                        return false;
                }

                return null;
            }

            if (!barge.IsPlayerCanInterract(player, false))
                return false;

            return null;
        }

        object CanChangeGrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)
        {
            if (player == null)
                return null;

            Barge barge = Barge.GetBargeByEntity(block);
            if (barge == null)
                return null;

            if (!barge.IsPlayerCanBuild(player, true))
                return false;

            if (block.grade == grade && _config.performanceConfig.autoSkin)
                return false;

            if (barge.IsBasicBuildingBlock(block))
                return false;

            if ((!ins._config.performanceConfig.allowStone && grade == BuildingGrade.Enum.Stone) || (!ins._config.performanceConfig.allowHqm && grade == BuildingGrade.Enum.TopTier))
            {
                NotifyManager.SendMessageToPlayer(player, "BlockGrade");
                return false;
            }
            else if ((!ins._config.performanceConfig.allowMetall && grade == BuildingGrade.Enum.Metal) || (!ins._config.performanceConfig.allowWood && grade == BuildingGrade.Enum.Wood))
            {
                return false;
            }

            return null;
        }

        object OnStructureRotate(BaseCombatEntity entity, BasePlayer player)
        {
            if (entity == null)
                return null;

            Barge barge = Barge.GetBargeByEntity(entity);
            if (barge == null)
                return null;

            if (!barge.IsStopped())
            {
                NotifyManager.SendMessageToPlayer(player, "AnchorBarge");
                return true;
            }

            return null;
        }

        void OnStructureUpgraded(BuildingBlock buildingBlock, BasePlayer player, BuildingGrade.Enum grade, ulong skin)
        {
            if (buildingBlock == null)
                return;

            Barge barge = Barge.GetBargeByEntity(buildingBlock);
            if (barge == null)
                return;

            if (!_config.performanceConfig.autoSkin)
                return;

            if (grade == BuildingGrade.Enum.Wood && skin != 10232)
            {
                buildingBlock.ChangeGradeAndSkin(BuildingGrade.Enum.Wood, 10232, true);
            }
            else if (grade == BuildingGrade.Enum.Metal && skin != 10221)
            {
                buildingBlock.ChangeGradeAndSkin(BuildingGrade.Enum.Metal, 10221, true);
                buildingBlock.SetCustomColour(11);
            }
        }

        void OnConstructionPlace(BaseCombatEntity entity, Construction component, Construction.Target constructionTarget, BasePlayer player)
        {
            if (entity == null || constructionTarget.entity == null || player == null)
                return;

            BaseEntity parentEntity = constructionTarget.entity.GetParentEntity();
            if (parentEntity == null || parentEntity.net == null)
                return;

            Barge barge = Barge.GetBargeByParentEntityNetId(parentEntity.net.ID.Value);
            if (barge == null)
                return;

            barge.OnPlayerBuild(player, entity, constructionTarget.entity);
        }

        object OnPoweredLightsPointAdd(PoweredLightsDeployer poweredLightsDeployer, BasePlayer player, Vector3 vector31, Vector3 vector32)
        {
            if (player == null)
                return null;

            Barge barge = Barge.GetBargeByCollider(player);
            if (barge == null)
                return null;

            NotifyManager.SendMessageToPlayer(player, "BlockedOnBarge");
            return true;
        }

        object OnEntityStabilityCheck(BuildingBlock buildingBlock)
        {
            if (buildingBlock == null || buildingBlock.net == null)
                return null;

            Barge barge = Barge.GetBargeByEntity(buildingBlock);

            if (barge == null)
                return null;

            return true;
        }

        void OnEntityKill(BuildingBlock buildingBlock)
        {
            if (buildingBlock == null || buildingBlock.net == null)
                return;

            Barge barge = Barge.GetBargeByEntity(buildingBlock);

            if (barge == null)
                return;

            barge.Invoke(() => barge.shoudUpdateStability = true, 0.1f);
        }

        object OnEntityKill(SkyLantern skyLantern)
        {
            if (skyLantern == null || skyLantern.net == null)
                return null;

            Barge barge = Barge.GetBargeByParentEntityNetId(skyLantern.net.ID.Value);
            if (barge == null)
                return null;

            if (!barge.shoudKill)
                return true;

            return null;
        }

        void OnEntitySpawned(BaseBoat baseBoat)
        {
            if (baseBoat == null)
                return;

            BoatConnectionTrigger.TryAddBoatConnectionTrigger(baseBoat);
        }

        void OnEntitySpawned(CargoShip cargoShip)
        {
            if (cargoShip == null)
                return;

            CargoShipManager.AttachController(cargoShip);
        }

        void OnEntitySpawned(DecayEntity decayEntity)
        {
            CheckIfOtherPluginSpawnedEntity(decayEntity);
        }

        void OnEntitySpawned(Recycler recycler)
        {
            CheckIfOtherPluginSpawnedEntity(recycler);
        }

        void OnEntitySpawned(BasePortal basePortal)
        {
            CheckIfOtherPluginSpawnedEntity(basePortal);
        }

        void CheckIfOtherPluginSpawnedEntity(BaseEntity baseEntity)
        {
            if (baseEntity == null || baseEntity.HasParent())
                return;

            if (baseEntity is StorageContainer && !baseEntity.enableSaving)
                return;

            Barge barge = Barge.GetBargeByCollider(baseEntity);
            if (barge == null)
                return;

            if (!barge.IsEntityShoudParrent(baseEntity) || barge.IsEntityShoutParentToTargetEntity(baseEntity))
                return;

            baseEntity.SetParent(barge.mainEntity, true);
        }

        object CanMountEntity(BasePlayer player, BaseMountable baseMountable)
        {
            if (player == null || baseMountable == null)
                return null;

            if ((baseMountable is BaseVehicleSeat && baseMountable.ShortPrefabName != "arcadeuser") || baseMountable is MovableBaseMountable)
                return null;

            Barge barge = Barge.GetBargeByEntity(baseMountable);
            if (barge == null)
            {
                BaseEntity parentEntity = baseMountable.VehicleParent();

                if (parentEntity == null)
                    return null;

                barge = Barge.GetBargeByEntity(parentEntity);
            }

            if (barge == null)
                return null;

            if (!barge.IsStopped())
            {
                NotifyManager.SendMessageToPlayer(player, "BlockedWhileMoving");
                return true;
            }

            return null;
        }

        void OnEntityMounted(BaseMountable baseMountable, BasePlayer player)
        {
            if (baseMountable == null || player == null)
                return;

            BaseVehicle baseVehicle = baseMountable.VehicleParent();

            if (baseVehicle == null || !baseVehicle.IsDriver(player))
                return;

            if (baseVehicle is not BaseBoat && baseVehicle is not BaseSubmarine)
                return;

            BaseEntity parentEntity = baseVehicle.GetParentEntity();
            if (parentEntity == null)
                return;

            Barge barge = Barge.GetBargeByEntity(parentEntity);
            if (barge == null)
                return;

            DockModule dockModule = parentEntity.GetComponentInChildren<DockModule>();

            if (dockModule == null || !barge.IsPlayerCanInterract(player, true))
                return;

            dockModule.ReleaseBoat();
        }

        void OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null || info.HitEntity == null)
                return;

            if (info.HitEntity.ShortPrefabName != "coaling_tower_fuel_storage.entity")
                return;

            Barge barge = Barge.GetBargeByEntity(info.HitEntity);
            if (barge == null || barge.IsStopped())
                return;

            BargePhysics bargePhysics = barge.GetBargePhysics();
            Vector3 pushDirection = (barge.transform.position - player.transform.position).normalized;
            pushDirection.y = 0;

            if (!barge.IsBargeOnShoal())
                return;

            if (TerrainMeta.HeightMap.GetHeight(barge.transform.position + pushDirection) > TerrainMeta.HeightMap.GetHeight(barge.transform.position))
                return;

            barge.StartMoving();
            bargePhysics.PushBarge(pushDirection);
        }

        object CanPickupEntity(BasePlayer player, SimpleLight simpleLight)
        {
            if (simpleLight == null || simpleLight.net == null)
                return null;

            Barge barge = Barge.GetBargeByEntity(simpleLight);
            if (barge == null)
                return null;

            if (simpleLight.GetParentEntity() is not DoorCloser)
                return null;

            return false;
        }

        object CanPickupEntity(BasePlayer player, PressButton preseButton)
        {
            if (preseButton == null)
                return null;

            if (preseButton.HasFlag(BaseEntity.Flags.InUse))
                return false;

            return true;
        }

        object OnWireClear(BasePlayer player, IOEntity entity1, int connected, IOEntity entity2, bool flag)
        {
            if (entity1 == null || entity2 == null)
                return null;

            Barge barge = Barge.GetBargeByEntity(entity1);
            if (barge == null)
                barge = Barge.GetBargeByEntity(entity2);

            if (barge == null)
                return null;

            if (!barge.IsStopped())
            {
                NotifyManager.SendMessageToPlayer(player, "BlockedWhileMoving");
                return true;
            }

            return null;
        }

        object OnWireConnect(BasePlayer player, IOEntity entity1, int inputs, IOEntity entity2, int outputs)
        {
            if (entity1 == null || entity2 == null)
                return null;

            Barge barge1 = Barge.GetBargeByEntity(entity1);
            Barge barge2 = Barge.GetBargeByEntity(entity2);

            if (barge1 != barge2)
                return true;

            if (barge1 == null)
                return null;

            if (!barge1.IsStopped())
            {
                NotifyManager.SendMessageToPlayer(player, "BlockedWhileMoving");
                return true;
            }

            return null;
        }

        void OnExplosiveThrown(BasePlayer player, RoadFlare roadFlare, ThrownWeapon thrownWeapon)
        {
            if (!player.IsRealPlayer() || roadFlare == null || thrownWeapon == null)
                return;

            Item item = thrownWeapon.GetItem();
            if (item == null)
                return;

            BargeConfig bargeConfig = _config.bargeConfigs.FirstOrDefault(x => x.itemConfig.shortname == item.info.shortname && x.itemConfig.skin == item.skin);
            if (bargeConfig == null)
                return;

            BargeCaller.Attach(roadFlare, player, bargeConfig);
        }

        object OnEntityTakeDamage(SkyLantern skyLantern, HitInfo info)
        {
            if (skyLantern == null || info == null)
                return null;

            Barge barge = Barge.GetBargeByEntity(skyLantern);
            if (barge == null)
                return null;

            return true;
        }

        object OnEntityTakeDamage(BuildingBlock buildingBlock, HitInfo info)
        {
            if (buildingBlock == null || info == null)
                return null;

            Barge barge = Barge.GetBargeByEntity(buildingBlock);
            if (barge == null)
                return null;

            if (barge.IsBasicBuildingBlock(buildingBlock))
                return true;

            return null;
        }

        object OnEntityTakeDamage(PressButton button, HitInfo info)
        {
            if (button == null || info == null)
                return null;

            Barge barge = Barge.GetBargeByEntity(button);
            if (barge == null)
                return null;

            BaseEntity parentEntity = button.GetParentEntity();
            if (parentEntity == null)
                return null;

            BaseModule baseModule = parentEntity.GetComponentInChildren<BaseModule>();
            if (baseModule == null)
                return null;

            return true;
        }

        object OnEntityTakeDamage(BaseBoat baseBoat, HitInfo info)
        {
            if (baseBoat == null || info == null)
                return null;

            BaseEntity parentEntity = baseBoat.GetParentEntity();
            if (parentEntity == null || parentEntity.net == null)
                return null;

            Barge barge = Barge.GetBargeByEntity(parentEntity);
            if (barge == null)
                return null;

            if (info.damageTypes.GetMajorityDamageType() == DamageType.Decay)
                return true;

            return null;
        }

        object OnEntityTakeDamage(BaseSubmarine baseSubmarine, HitInfo info)
        {
            if (baseSubmarine == null || info == null)
                return null;

            BaseEntity parentEntity = baseSubmarine.GetParentEntity();
            if (parentEntity == null || parentEntity.net == null)
                return null;

            Barge barge = Barge.GetBargeByEntity(parentEntity);
            if (barge == null)
                return null;

            if (info.damageTypes.GetMajorityDamageType() == DamageType.Decay)
                return true;

            return null;
        }

        object CanLootEntity(BasePlayer player, ResourceExtractorFuelStorage container)
        {
            if (container == null || player == null)
                return null;

            Barge barge = Barge.GetBargeByEntity(container);
            if (barge == null)
                return null;

            if (container.GetParentEntity() is DoorCloser && !barge.IsPlayerCanInterract(player, true))
                return true;

            return null;
        }

        void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (!player.IsRealPlayer() || newItem == null)
                return;

            if (newItem.info.shortname == "storageadaptor")
                AdaptorInstallController.TryAttachController(player);
        }


        object CanEntityTakeDamage(SkyLantern skyLantern, HitInfo info)
        {
            if (skyLantern == null || info == null)
                return null;

            Barge barge = Barge.GetBargeByEntity(skyLantern);
            if (barge == null)
                return null;

            return false;
        }

        object CanEntityTakeDamage(BuildingBlock buildingBlock, HitInfo info)
        {
            if (buildingBlock == null || info == null)
                return null;

            Barge barge = Barge.GetBargeByEntity(buildingBlock);
            if (barge == null)
                return null;

            if (barge.IsBasicBuildingBlock(buildingBlock))
                return false;

            return null;
        }

        object CanEntityTakeDamage(PressButton button, HitInfo info)
        {
            if (button == null || info == null)
                return null;

            Barge barge = Barge.GetBargeByEntity(button);
            if (barge == null)
                return null;

            BaseEntity parentEntity = button.GetParentEntity();
            if (parentEntity == null)
                return null;

            BaseModule baseModule = parentEntity.GetComponentInChildren<BaseModule>();
            if (baseModule == null)
                return null;

            return false;
        }

        object CanEntityTakeDamage(BaseBoat baseBoat, HitInfo info)
        {
            if (baseBoat == null || info == null)
                return null;

            BaseEntity parentEntity = baseBoat.GetParentEntity();
            if (parentEntity == null || parentEntity.net == null)
                return null;

            Barge barge = Barge.GetBargeByEntity(parentEntity);
            if (barge == null)
                return null;

            if (info.damageTypes.GetMajorityDamageType() == DamageType.Decay)
                return false;

            return null;
        }

        object CanEntityTakeDamage(BaseSubmarine baseSubmarine, HitInfo info)
        {
            if (baseSubmarine == null || info == null)
                return null;

            BaseEntity parentEntity = baseSubmarine.GetParentEntity();
            if (parentEntity == null || parentEntity.net == null)
                return null;

            Barge barge = Barge.GetBargeByEntity(parentEntity);
            if (barge == null)
                return null;

            if (info.damageTypes.GetMajorityDamageType() == DamageType.Decay)
                return false;

            return null;
        }
        #endregion Hooks

        #region Commands
        [ChatCommand("spawnbarge")]
        void ChatSpawnBargeCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin)
                return;

            string bargePreset = "";

            if (arg.Length > 0)
                bargePreset = arg[0];

            Barge.SpawnBarge(player.transform.position, Quaternion.identity, bargePreset, false);
        }

        [ChatCommand("killbarge")]
        void ChatKillCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin)
                return;

            BaseEntity target = RaycastAll<BaseEntity>(player.eyes.HeadRay());
            if (target == null)
                return;

            Barge barge = Barge.GetBargeByEntity(target);
            if (barge == null)
                return;

            barge.KillBarge();
        }

        [ChatCommand("killallbarges")]
        void ChatKillAllCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin)
                return;

            Barge.KillAllBarges();
        }

        [ChatCommand("bargetest")]
        void ChatTestCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin)
                return;

            
        }

        [ChatCommand("givebarge")]
        void ChatGiveBargeCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin || arg.Length < 1)
                return;

            string presetName = arg[0];
            BargeConfig bargeConfig = _config.bargeConfigs.FirstOrDefault(x => x.presetName == presetName);
            if (bargeConfig == null)
            {
                NotifyManager.PrintError(player, "ConfigNotFound_Exeption", presetName);
                return;
            }

            LootManager.GiveItemToPLayer(player, bargeConfig.itemConfig, 1);
            NotifyManager.SendMessageToPlayer(player, "GotBarge");
        }

        [ConsoleCommand("givebarge")]
        void ConsoleGiveBargeCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null || arg.Args.Length < 2)
                return;

            string presetName = arg.Args[0];
            BargeConfig bargeConfig = _config.bargeConfigs.FirstOrDefault(x => x.presetName == presetName);
            if (bargeConfig == null)
            {
                NotifyManager.PrintError(null, "ConfigNotFound_Exeption", presetName);
                return;
            }

            ulong targetUserId = Convert.ToUInt64(arg.Args[1]);
            BasePlayer targetPlayer = BasePlayer.FindByID(targetUserId);
            if (targetPlayer == null)
            {
                NotifyManager.PrintError(null, "PlayerNotFound_Exeption", arg.Args[1]);
                return;
            }

            LootManager.GiveItemToPLayer(targetPlayer, bargeConfig.itemConfig, 1);
            NotifyManager.SendMessageToPlayer(targetPlayer, "GotBarge");
        }

        [ConsoleCommand("savebarge")]
        void ConsoleSaveBargeCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                return;

            MapSaver.CreateOrAddNewData("new");
        }
        #endregion Commands

        #region Methods
        void UpdateConfig()
        {
            if (_config.version != Version)
            {
                PluginConfig defaultConfig = PluginConfig.DefaultConfig();

                if (_config.version.Patch <= 1)
                {
                    foreach (BargeConfig bargeConfig in _config.bargeConfigs)
                    {
                        bargeConfig.engineConfig.rotateScale = 1f;
                    }
                }
                if (_config.version.Patch <= 4)
                {
                    _config.mainConfig.blockedCommands = defaultConfig.mainConfig.blockedCommands;
                }

                _config.version = Version;
                SaveConfig();
            }
        }

        void Unsubscribes()
        {
            foreach (string hook in subscribeMethods)
                Unsubscribe(hook);
        }

        void Subscribes()
        {
            foreach (string hook in subscribeMethods)
                Subscribe(hook);
        }

        static void Debug(params object[] arg)
        {
            string result = "";

            foreach (object obj in arg)
                if (obj != null)
                    result += obj.ToString() + " ";

            ins.Puts(result);
        }

        BaseEntity RaycastAll<T>(Ray ray, float distance = 50) where T : BaseEntity
        {
            RaycastHit[] hits = Physics.RaycastAll(ray);
            GamePhysics.Sort(hits);
            BaseEntity target = null;

            foreach (RaycastHit hit in hits)
            {
                BaseEntity ent = hit.GetEntity();

                if (ent is T && hit.distance < distance)
                {
                    target = ent;
                    break;
                }
            }

            return target;
        }
        #endregion Methods

        #region Classes
        static class BargeSpawner
        {
            static Coroutine spawnCorountine;
            static HashSet<SpawnPositionInfo> spawnLocations = new HashSet<SpawnPositionInfo>();

            internal static void StartPeriodicSpawn()
            {
                if (!ins._config.spawnConfig.isSpawnEnabled)
                    return;

                CacheSuitableMonuments();

                if (spawnLocations.Count == 0)
                    return;

                spawnCorountine = ServerMgr.Instance.StartCoroutine(SpawnCorountine());
            }

            static void CacheSuitableMonuments()
            {
                spawnLocations = new HashSet<SpawnPositionInfo>();

                foreach (MonumentInfo monumentInfo in TerrainMeta.Path.Monuments)
                {
                    if (monumentInfo == null || monumentInfo.transform == null)
                        continue;

                    SpawnMonumentConfig spawnMonumentConfig = ins._config.spawnConfig.monuments.FirstOrDefault(x => x.isEnabled && x.monumentName == monumentInfo.name);
                    if (spawnMonumentConfig == null)
                        continue;

                    SaveMonumentPositions(monumentInfo, spawnMonumentConfig);
                }
            }

            static void SaveMonumentPositions(MonumentInfo monumentInfo, SpawnMonumentConfig spawnMonumentConfig)
            {
                foreach (LocationConfig locationConfig in spawnMonumentConfig.locations)
                {
                    Vector3 localPosition = locationConfig.position.ToVector3();
                    Vector3 localRotation = locationConfig.rotation.ToVector3();

                    Vector3 globalPosition = PositionDefiner.GetGlobalPosition(monumentInfo.transform, localPosition);
                    Quaternion globalRotation = PositionDefiner.GetGlobalRotation(monumentInfo.transform, localRotation);

                    SpawnPositionInfo spawnPositionInfo = new SpawnPositionInfo(globalPosition, globalRotation);
                    spawnLocations.Add(spawnPositionInfo);
                }
            }

            static IEnumerator SpawnCorountine()
            {
                while (true)
                {
                    yield return CoroutineEx.waitForSeconds(UnityEngine.Random.Range(ins._config.spawnConfig.minSpawnTime, ins._config.spawnConfig.maxSpawnTime));

                    List<SpawnPositionInfo> suitablePositions = Pool.Get<List<SpawnPositionInfo>>();

                    foreach (SpawnPositionInfo suitablePosition in spawnLocations)
                        if (IsPositionSuitable(suitablePosition.position))
                            suitablePositions.Add(suitablePosition);

                    if (suitablePositions.Count > 0)
                    {
                        SpawnPositionInfo spawnPositionInfo = suitablePositions.GetRandom();
                        SpawnBarge(spawnPositionInfo);
                    }

                    Pool.FreeUnmanaged(ref suitablePositions);
                }
            }

            static void SpawnBarge(SpawnPositionInfo spawnPositionInfo)
            {
                if (Barge.GetBargePopulation(true) >= ins._config.spawnConfig.maxBargeCount)
                    return;

                string bargePresetName = GetRandomBargePreset();

                if (bargePresetName == null)
                    return;

                Barge.SpawnBarge(spawnPositionInfo.position, spawnPositionInfo.rotation, bargePresetName, true);
            }

            static string GetRandomBargePreset()
            {
                float sumChance = 0;

                foreach (var pair in ins._config.spawnConfig.probabilities)
                    sumChance += pair.Value;

                float random = UnityEngine.Random.Range(0, sumChance);

                foreach (var pair in ins._config.spawnConfig.probabilities)
                {
                    random -= pair.Value;

                    if (random <= 0)
                        return pair.Key;
                }

                return null;
            }

            static bool IsPositionSuitable(Vector3 position)
            {
                if (Barge.barges.Any(x => x != null && Vector3.Distance(x.transform.position, position) < 40f))
                    return false;

                HashSet<BaseEntity> entitiesForKill = new HashSet<BaseEntity>();

                foreach (Collider collider in UnityEngine.Physics.OverlapSphere(position, 20f))
                {
                    if (collider == null)
                        continue;

                    BaseEntity entity = collider.ToBaseEntity();

                    if (entity == null)
                        continue;

                    if (entity is BaseBoat or BaseSubmarine or CargoShip or BasePlayer)
                        return false;
                    else if (entity is JunkPile or DiveSite)
                        entitiesForKill.Add(entity);
                }

                foreach (BaseEntity entity in entitiesForKill)
                    if (entity.IsExists())
                        entity.Kill();

                return true;
            }

            internal static void StopSpawning()
            {
                if (spawnLocations != null)
                {
                    spawnLocations.Clear();
                    spawnLocations = null;
                }

                if (spawnCorountine != null)
                    ServerMgr.Instance.StopCoroutine(spawnCorountine);

                spawnCorountine = null;
            }
        }

        class SpawnPositionInfo
        {
            internal Vector3 position;
            internal Quaternion rotation;

            internal SpawnPositionInfo(Vector3 position, Quaternion rotation)
            {
                this.position = position;
                this.rotation = rotation;
            }
        }

        class FishingVilageZoneController : FacepunchBehaviour
        {
            static HashSet<FishingVilageZoneController> zoneControllers = new HashSet<FishingVilageZoneController>();
            SphereCollider sphereCollider;
            const float zoneRadius = 55;

            internal static void CacheVilages()
            {
                if (!ins._config.mainConfig.blockFishingVillage)
                    return;

                foreach (MonumentInfo monumentInfo in TerrainMeta.Path.Monuments)
                {
                    if (monumentInfo.name.Contains("fishing"))
                    {
                        GameObject gameObject = new GameObject("FishingVilageZoneController");
                        gameObject.layer = (int)Rust.Layer.Reserved1;
                        gameObject.transform.position = monumentInfo.transform.position;
                        FishingVilageZoneController zoneController = gameObject.AddComponent<FishingVilageZoneController>();
                        FishingVilageZoneController.zoneControllers.Add(zoneController);
                    }
                }
            }

            void Awake()
            {
                sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.isTrigger = true;
                sphereCollider.radius = zoneRadius;
            }

            void OnTriggerEnter(Collider other)
            {
                if (other == null)
                    return;

                BaseEntity entity = other.ToBaseEntity();
                if (entity == null || entity.net == null || entity.ShortPrefabName != "kayak")
                    return;

                Barge barge = Barge.GetBargeByPhysicsEntityNetId(entity.net.ID.Value);

                if (barge == null)
                    return;

                BargePhysics bargePhysics = barge.GetBargePhysics();
                bargePhysics.OnBargeEnterToBlockZoneZone(transform.position);
            }

            internal static void ClearZones()
            {
                foreach (FishingVilageZoneController zoneController in zoneControllers)
                    if (zoneController != null)
                        zoneController.DeleteZone();
            }

            void DeleteZone()
            {
                Destroy(this.gameObject);
            }
        }

        class BuildingVisibilityManager : FacepunchBehaviour
        {
            static HashSet<BuildingVisibilityManager> buildingVisibilityUpdaters = new HashSet<BuildingVisibilityManager>();
            static HashSet<BuildingBlockModelInfo> buildingBlockModelInfos = new HashSet<BuildingBlockModelInfo>
            {
                new BuildingBlockModelInfo
                {
                    prefab = "assets/prefabs/building core/wall/wall.prefab",
                    grade = BuildingGrade.Enum.Metal,
                    badModelStates = new HashSet<int> { 9, 17, 25 },
                    goodModelState = 1
                },
                new BuildingBlockModelInfo
                {
                    prefab = "assets/prefabs/building core/roof/roof.prefab",
                    grade = BuildingGrade.Enum.Metal,
                    badModelStates = new HashSet<int> { 12, 20, 28, 36, 44, 60, 68, 76, 84, 92, 100, 108, 124 },
                    goodModelState = 4
                },
                new BuildingBlockModelInfo
                {
                    prefab = "assets/prefabs/building core/roof/roof.prefab",
                    grade = BuildingGrade.Enum.Metal,
                    badModelStates = new HashSet<int> { 8228, 8260, 8292, 24620, 24652, 24684, 24588, },
                    goodModelState = 8196
                },
                new BuildingBlockModelInfo
                {
                    prefab = "assets/prefabs/building core/roof/roof.prefab",
                    grade = BuildingGrade.Enum.Metal,
                    badModelStates = new HashSet<int> {32852, 32788, 32836, 98316, 98332, 98380, 98396 },
                    goodModelState = 32772
                },
                new BuildingBlockModelInfo
                {
                    prefab = "assets/prefabs/building core/roof/roof.prefab",
                    grade = BuildingGrade.Enum.Metal,
                    badModelStates = new HashSet<int> {33, 65, 97},
                    goodModelState = 1
                },
                new BuildingBlockModelInfo
                {
                    prefab = "assets/prefabs/building core/roof/roof.prefab",
                    grade = BuildingGrade.Enum.Metal,
                    badModelStates = new HashSet<int> {18, 66, 82},
                    goodModelState = 2
                },
                new BuildingBlockModelInfo
                {
                    prefab = "assets/prefabs/building core/roof/roof.prefab",
                    grade = BuildingGrade.Enum.Metal,
                    badModelStates = new HashSet<int> {67},
                    goodModelState = 3
                },
                new BuildingBlockModelInfo
                {
                    prefab = "assets/prefabs/building core/roof.triangle/roof.triangle.prefab",
                    grade = BuildingGrade.Enum.Metal,
                    badModelStates = new HashSet<int> {3, 5, 7, 9, 11, 13, 15 },
                    goodModelState = 1
                },
                new BuildingBlockModelInfo
                {
                    prefab = "assets/prefabs/building core/roof.triangle/roof.triangle.prefab",
                    grade = BuildingGrade.Enum.Metal,
                    badModelStates = new HashSet<int> {25, 49, 51, 57, 59, 8392713},
                    goodModelState = 17
                },
                new BuildingBlockModelInfo
                {
                    prefab = "assets/prefabs/building core/wall.half/wall.half.prefab",
                    grade = BuildingGrade.Enum.Metal,
                    badModelStates = new HashSet<int> {1, 2, 3},
                    goodModelState = 0
                },
                new BuildingBlockModelInfo
                {
                    prefab = "assets/prefabs/building core/wall.low/wall.low.prefab",
                    grade = BuildingGrade.Enum.Metal,
                    badModelStates = new HashSet<int> {1, 2, 3},
                    goodModelState = 0
                },
                new BuildingBlockModelInfo
                {
                    prefab = "assets/prefabs/building core/wall.window/wall.window.prefab",
                    grade = BuildingGrade.Enum.Metal,
                    badModelStates = new HashSet<int> {1, 2},
                    goodModelState = 0
                },
                new BuildingBlockModelInfo
                {
                    prefab = "assets/prefabs/building core/wall.doorway/wall.doorway.prefab",
                    grade = BuildingGrade.Enum.Metal,
                    badModelStates = new HashSet<int> {1, 2},
                    goodModelState = 0
                }
            };

            BaseEntity parentEntity;
            Coroutine badBuildingBlocksCorountine;
            Coroutine goodBuildingBlocksCorountine;
            HashSet<BuildingBlock> badBuildingBlocks = new HashSet<BuildingBlock>();
            HashSet<BuildingBlock> goodBuildingBlocks = new HashSet<BuildingBlock>();
            int updatesPerTick;
            internal HashSet<IoEntitySaveData> ioEntitySaveDatas = new HashSet<IoEntitySaveData>();

            internal static void UpdateBuildingBlockVisibility(BuildingBlock buildingBlock)
            {
                if (buildingBlock.net.group.subscribers.Count == 0)
                    return;

                NetWrite newWrite = Net.sv.StartWrite();
                newWrite.PacketID(Message.Type.RPCMessage);
                newWrite.EntityID(buildingBlock.net.ID);
                newWrite.UInt32(StringPool.Get("RefreshSkin"));
                newWrite.UInt64(0);
                newWrite.Send(new SendInfo(buildingBlock.net.group.subscribers));
            }

            internal static BuildingVisibilityManager AttachVisibilityUpdater(BaseEntity parentEntity)
            {
                GameObject triggerGameObject = new GameObject("BuildingVisibilityUpdater");
                triggerGameObject.transform.SetParent(parentEntity.transform, false);
                BuildingVisibilityManager buildingVisibilityUpdater = triggerGameObject.AddComponent<BuildingVisibilityManager>();
                buildingVisibilityUpdater.Init(parentEntity);
                buildingVisibilityUpdaters.Add(buildingVisibilityUpdater);
                return buildingVisibilityUpdater;
            }

            internal void UpdateBuildingBlockModels()
            {
                foreach (BaseEntity entity in parentEntity.children)
                {
                    BuildingBlock buildingBlock = entity as BuildingBlock;

                    if (buildingBlock != null)
                    {
                        UpdateBuildingBlockModel(buildingBlock);
                    }
                }
            }

            static void DefineUpdateFrequency()
            {
                buildingVisibilityUpdaters.RemoveWhere(x => x == null);

                int sumBadBuildinbBlocksCount = 0;

                foreach (BuildingVisibilityManager buildingVisibilityUpdater in buildingVisibilityUpdaters)
                    if (buildingVisibilityUpdater != null)
                        sumBadBuildinbBlocksCount += buildingVisibilityUpdater.badBuildingBlocks.Count;

                foreach (BuildingVisibilityManager buildingVisibilityUpdater in buildingVisibilityUpdaters)
                {
                    if (buildingVisibilityUpdater == null)
                        continue;

                    buildingVisibilityUpdater.updatesPerTick = sumBadBuildinbBlocksCount == 0 ? 0 : buildingVisibilityUpdater.badBuildingBlocks.Count * ins._config.performanceConfig.updatePerTick / sumBadBuildinbBlocksCount;

                    if (buildingVisibilityUpdater.updatesPerTick > buildingVisibilityUpdater.badBuildingBlocks.Count)
                        buildingVisibilityUpdater.updatesPerTick = buildingVisibilityUpdater.badBuildingBlocks.Count;
                }
            }

            void Init(BaseEntity parentEntity)
            {
                this.parentEntity = parentEntity;
            }

            internal void OnStartMoving()
            {
                badBuildingBlocks.Clear();
                goodBuildingBlocks.Clear();
                ioEntitySaveDatas.Clear();

                foreach (BaseEntity entity in parentEntity.children)
                {
                    BuildingBlock buildingBlock = entity as BuildingBlock;
                    if (buildingBlock != null)
                    {
                        CacheBuildingBlock(buildingBlock);
                        continue;
                    }

                    IOEntity iOEntity = entity as IOEntity;
                    if (iOEntity != null)
                    {
                        CacheIOEntity(iOEntity);
                        continue;
                    }

                    if (entity is DoorCloser)
                    {
                        foreach (BaseEntity subChildren in entity.children)
                        {
                            BuildingBlock subBuildingBlock = subChildren as BuildingBlock;
                            if (subBuildingBlock != null)
                                CacheBuildingBlock(subBuildingBlock);
                        }
                    }
                }

                StopCorountines();
                badBuildingBlocksCorountine = ServerMgr.Instance.StartCoroutine(BadBuildingBlocksCorountine());
                goodBuildingBlocksCorountine = ServerMgr.Instance.StartCoroutine(GoodBuildingBlocksCorountine());
                DefineUpdateFrequency();
            }

            void CacheBuildingBlock(BuildingBlock buildingBlock)
            {
                UpdateBuildingBlockModel(buildingBlock);

                if (IsBuildingBlockGoodMoving(buildingBlock))
                    goodBuildingBlocks.Add(buildingBlock);
                else
                    badBuildingBlocks.Add(buildingBlock);
            }

            void UpdateBuildingBlockModel(BuildingBlock buildingBlock)
            {
                BuildingBlockModelInfo buildingBlockModelInfo = buildingBlockModelInfos.FirstOrDefault(x => x.prefab == buildingBlock.PrefabName && x.badModelStates.Contains(buildingBlock.modelState));

                if (buildingBlockModelInfo == null)
                    return;

                buildingBlock.SetConditionalModel(buildingBlockModelInfo.goodModelState);
                buildingBlock.SendNetworkUpdate();
            }

            bool IsBuildingBlockGoodMoving(BuildingBlock buildingBlock)
            {
                if (buildingBlock.grade == BuildingGrade.Enum.Wood && buildingBlock.skinID == 10232)
                    return true;

                if (buildingBlock.grade == BuildingGrade.Enum.Metal && buildingBlock.skinID == 10221)
                {
                    if (buildingBlock.ShortPrefabName.Contains("floor"))
                        return false;
                    else
                        return true;
                }

                return false;
            }

            void CacheIOEntity(IOEntity iOEntity)
            {
                HashSet<IOSlotData> inputSlotDatas = new HashSet<IOSlotData>();
                HashSet<IOSlotData> outputSlotDatas = new HashSet<IOSlotData>();

                foreach (var input in iOEntity.inputs)
                {
                    IOSlotData iOSlotData = GetSlotData(input);
                    inputSlotDatas.Add(iOSlotData);
                }

                foreach (var output in iOEntity.outputs)
                {
                    IOSlotData iOSlotData = GetSlotData(output);
                    outputSlotDatas.Add(iOSlotData);
                }

                iOEntity.SendNetworkUpdate();

                if (inputSlotDatas.Count == 0 && outputSlotDatas.Count == 0)
                    return;

                IoEntitySaveData ioEntitySaveData = new IoEntitySaveData(iOEntity.net.ID.Value, inputSlotDatas, outputSlotDatas);
                ioEntitySaveDatas.Add(ioEntitySaveData);
            }

            IOSlotData GetSlotData(IOEntity.IOSlot slot)
            {
                HashSet<Vector3> linePoints = new HashSet<Vector3>();

                if (slot.linePoints != null)
                    foreach (Vector3 localPosition in slot.linePoints)
                        linePoints.Add(localPosition);

                slot.linePoints = new Vector3[1] { Vector3.up };
                return new IOSlotData(linePoints);
            }

            IEnumerator BadBuildingBlocksCorountine()
            {
                while (true)
                {
                    int updatesInFrame = updatesPerTick;

                    foreach (BuildingBlock buildingBlock in badBuildingBlocks)
                    {
                        if (!buildingBlock.IsExists() || buildingBlock.IsDead())
                            continue;

                        updatesInFrame--;
                        UpdateBuildingBlockVisibility(buildingBlock);

                        if (updatesInFrame <= 0)
                        {
                            updatesInFrame = updatesPerTick;
                            yield return CoroutineEx.waitForSeconds(0.05f);
                        }
                    }

                    yield return CoroutineEx.waitForSeconds(0.05f);
                }
            }

            IEnumerator GoodBuildingBlocksCorountine()
            {
                while (true)
                {
                    foreach (BuildingBlock buildingBlock in goodBuildingBlocks)
                    {
                        if (!buildingBlock.IsExists() || buildingBlock.IsDead())
                            continue;

                        UpdateBuildingBlockVisibility(buildingBlock);

                        yield return CoroutineEx.waitForSeconds(0.025f);
                    }

                    yield return CoroutineEx.waitForSeconds(2.5f);
                }
            }

            internal void OnStopMoving()
            {
                StopCorountines();

                foreach (BaseEntity entity in parentEntity.children)
                {
                    BuildingBlock buildingBlock = entity as BuildingBlock;
                    if (buildingBlock != null && buildingBlock.net != null)
                    {
                        if (IsBuildingBlockGoodMoving(buildingBlock) && !buildingBlock.ShortPrefabName.Contains("stair"))
                        {
                            UpdateBuildingBlockVisibility(buildingBlock);
                        }

                        buildingBlock.limitNetworking = true;
                        buildingBlock.limitNetworking = false;
                        buildingBlock.SetConditionalModel(buildingBlock.currentSkin.DetermineConditionalModelState(buildingBlock));
                        buildingBlock.SendNetworkUpdate();

                        continue;
                    }

                    IOEntity iOEntity = entity as IOEntity;
                    if (iOEntity != null && iOEntity.net != null)
                    {
                        IoEntitySaveData ioEntitySaveData = ioEntitySaveDatas.FirstOrDefault(x => x.ioEntityNetId == iOEntity.net.ID.Value);
                        if (ioEntitySaveData != null)
                            ResetWires(iOEntity, ioEntitySaveData);

                        iOEntity.limitNetworking = true;
                        iOEntity.limitNetworking = false;

                        continue;
                    }
                }

                DefineUpdateFrequency();
            }

            void ResetWires(IOEntity iOEntity, IoEntitySaveData ioEntitySaveData)
            {
                int counter = 0;
                foreach (IOSlotData slotData in ioEntitySaveData.inputSlotDatas)
                {
                    if (counter >= iOEntity.inputs.Length)
                        break;

                    IOEntity.IOSlot slot = iOEntity.inputs[counter];

                    if (slot == null)
                        break;

                    ResetSlot(iOEntity, slot, slotData);
                    counter++;
                }

                counter = 0;
                foreach (IOSlotData slotData in ioEntitySaveData.outputSlotDatas)
                {
                    if (counter >= iOEntity.outputs.Length)
                        break;

                    IOEntity.IOSlot slot = iOEntity.outputs[counter];

                    if (slot == null)
                        break;

                    ResetSlot(iOEntity, slot, slotData);
                    counter++;
                }

                iOEntity.SendNetworkUpdate();
            }

            void ResetSlot(IOEntity iOEntity, IOEntity.IOSlot slot, IOSlotData iOSlotData)
            {
                slot.originPosition = iOEntity.transform.position;
                slot.originRotation = iOEntity.transform.rotation.eulerAngles;

                Vector3[] array = new Vector3[iOSlotData.linePoints.Count];
                int counter = 0;

                foreach (Vector3 linePoint in iOSlotData.linePoints)
                {
                    array[counter] = linePoint;
                    counter++;
                }

                slot.linePoints = array;
            }

            internal void DestroyUpdater()
            {
                StopCorountines();
                Destroy(this.gameObject);
            }

            void StopCorountines()
            {
                if (badBuildingBlocksCorountine != null)
                    ServerMgr.Instance.StopCoroutine(badBuildingBlocksCorountine);

                if (goodBuildingBlocksCorountine != null)
                    ServerMgr.Instance.StopCoroutine(goodBuildingBlocksCorountine);
            }

            void OnDestroy()
            {
                StopCorountines();
            }

            class BuildingBlockModelInfo
            {
                internal string prefab;
                internal BuildingGrade.Enum grade;
                internal HashSet<int> badModelStates;
                internal int goodModelState;
            }
        }

        class AdaptorInstallController : FacepunchBehaviour
        {
            static HashSet<AdaptorInstallController> adaptorControllers = new HashSet<AdaptorInstallController>();
            BasePlayer player;
            Barge barge;
            StorageContainer fakeContainer;
            StorageContainer targetContainer;

            internal static void TryAttachController(BasePlayer player)
            {
                AdaptorInstallController adaptorInstallController = adaptorControllers.FirstOrDefault(x => x.player != null && x.player.userID == player.userID);
                if (adaptorInstallController != null)
                    return;

                Barge barge = Barge.GetBargeByCollider(player);
                if (barge == null || !barge.IsStopped())
                    return;

                adaptorInstallController = player.gameObject.AddComponent<AdaptorInstallController>();
                adaptorInstallController.Init(player, barge);
                adaptorControllers.Add(adaptorInstallController);
            }

            void Init(BasePlayer player, Barge barge)
            {
                this.player = player;
                this.barge = barge;
            }

            void FixedUpdate()
            {
                if (player == null || player.IsSleeping() || !player.IsConnected || player.IsWounded() || !barge.IsStopped())
                {
                    DestroyController();
                    return;
                }

                Item item = player.GetActiveItem();
                if (item == null || item.info == null || item.info.shortname != "storageadaptor")
                {
                    DestroyController();
                    return;
                }

                StorageContainer storageContainer = ins.RaycastAll<BaseEntity>(player.eyes.HeadRay(), 5) as StorageContainer;
                if (storageContainer == null)
                {
                    TryTransferArapters();
                    return;
                }
                else if (!storageContainer.HasParent())
                    return;

                if (storageContainer != targetContainer)
                {
                    TryTransferArapters();
                    targetContainer = storageContainer;
                    fakeContainer = BuildManager.SpawnRegularEntity(storageContainer.PrefabName, storageContainer.transform.position, storageContainer.transform.rotation, 123, enableSaving: false) as StorageContainer;
                }
            }

            void TryTransferArapters()
            {
                if (!fakeContainer.IsExists() || !fakeContainer.IsExists() || targetContainer.transform.position != fakeContainer.transform.position)
                    return;

                foreach (BaseEntity entity in fakeContainer.children.ToHashSet())
                    if (entity != null && entity is IndustrialStorageAdaptor)
                        entity.SetParent(targetContainer);

                fakeContainer.Kill();
                fakeContainer = null;
                targetContainer = null;
            }

            internal static void OnPluginUnload()
            {
                foreach (AdaptorInstallController adaptorInstallController in adaptorControllers)
                    if (adaptorInstallController != null)
                        adaptorInstallController.DestroyController();
            }

            void DestroyController()
            {
                TryTransferArapters();

                if (fakeContainer.IsExists())
                    fakeContainer.Kill();

                Destroy(this);
            }
        }

        static class StabilityManager
        {
            internal static void UpdateChildEntitiesStability(BaseEntity parentEntity)
            {
                HashSet<BuildingBlock> allBuildingBlocks = new HashSet<BuildingBlock>();

                foreach (BaseEntity entity in parentEntity.children)
                {
                    BuildingBlock buildingBlock = entity as BuildingBlock;

                    if (!buildingBlock.IsExists() || buildingBlock.grounded)
                        continue;

                    allBuildingBlocks.Add(buildingBlock);
                }

                allBuildingBlocks.OrderBy(x => x.transform.localPosition.y);

                foreach (BuildingBlock buildingBlock in allBuildingBlocks)
                    StabilityCheck(buildingBlock);

                UpdateGroundEntities(parentEntity);
            }

            static void UpdateGroundEntities(BaseEntity parentEntity)
            {
                for (int i = 0; i < parentEntity.children.Count; i++)
                {
                    DecayEntity decayEntity = parentEntity.children[i] as DecayEntity;

                    if (!decayEntity.IsExists() || decayEntity is BuildingBlock)
                        continue;

                    decayEntity.parentEntity.Set(null);
                    decayEntity.BroadcastMessage("OnPhysicsNeighbourChanged", SendMessageOptions.DontRequireReceiver);
                    decayEntity.SetParent(parentEntity, false, true);
                }
            }

            static void StabilityCheck(StabilityEntity stabilityEntity)
            {
                if (stabilityEntity.grounded)
                    return;

                int distanceFromGround = stabilityEntity.DistanceFromGround(null);

                if (distanceFromGround != stabilityEntity.cachedDistanceFromGround)
                    stabilityEntity.cachedDistanceFromGround = distanceFromGround;

                float supportValue = SupportValue(stabilityEntity);

                if (supportValue <= ConVar.Stability.collapse)
                {
                    stabilityEntity.Kill(BaseNetworkable.DestroyMode.Gib);
                    return;
                }

                if (Mathf.Abs(stabilityEntity.cachedStability - supportValue) > ConVar.Stability.accuracy)
                    stabilityEntity.cachedStability = supportValue;

                stabilityEntity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            }

            static float SupportValue(StabilityEntity buildingBlock)
            {
                if (buildingBlock.grounded)
                    return 1f;

                List<Support> supports = GetSupports(buildingBlock);
                float result = 0f;

                for (int i = 0; i < supports.Count; i++)
                {
                    Support support = supports[i];
                    StabilityEntity stabilityEntity = support.SupportEntity(buildingBlock);

                    if (stabilityEntity != null)
                    {
                        float supportValue = CachedSupportValue(stabilityEntity);

                        if (supportValue != 0f)
                            result += supportValue * support.factor;
                    }
                }

                return Mathf.Clamp01(result);
            }

            static float CachedSupportValue(StabilityEntity buildingBlock)
            {
                if (buildingBlock.grounded)
                    return 1f;

                List<Support> supports = GetSupports(buildingBlock);
                float supportValue = 0f;

                for (int i = 0; i < supports.Count; i++)
                {
                    Support item = supports[i];
                    StabilityEntity stabilityEntity = item.SupportEntity(buildingBlock);
                    if (stabilityEntity != null)
                    {
                        float cachedStability = stabilityEntity.cachedStability;

                        if (cachedStability != 0f)
                            supportValue += cachedStability * item.factor;
                    }
                }
                return Mathf.Clamp01(supportValue);
            }

            static List<Support> GetSupports(StabilityEntity buildingBlock)
            {
                List<Support> supports = new List<Support>();

                List<EntityLink> entityLinks = buildingBlock.GetEntityLinks();
                for (int i = 0; i < entityLinks.Count; i++)
                {
                    EntityLink entityLink = entityLinks[i];
                    if (entityLink.IsMale())
                    {
                        if (entityLink.socket is StabilitySocket)
                        {
                            supports.Add(new Support(buildingBlock, entityLink, (entityLink.socket as StabilitySocket).support));
                        }

                        if (entityLink.socket is ConstructionSocket)
                        {
                            supports.Add(new Support(buildingBlock, entityLink, (entityLink.socket as ConstructionSocket).support));
                        }
                    }
                }

                return supports;
            }
        }

        class CargoShipManager : FacepunchBehaviour
        {
            static HashSet<CargoShipManager> cargoShipManagers = new HashSet<CargoShipManager>();
            CargoShip cargoShip;

            internal static void UpdateAllCargos()
            {
                HashSet<CargoShip> cargoShips = BaseNetworkable.serverEntities.OfType<CargoShip>();

                foreach (CargoShip cargoShip in cargoShips)
                    if (cargoShip != null)
                        AttachController(cargoShip);
            }

            internal static CargoShipManager AttachController(CargoShip cargoShip)
            {
                CargoShipManager cargoShipManager = cargoShip.gameObject.AddComponent<CargoShipManager>();
                cargoShipManager.Init(cargoShip);
                return cargoShipManager;
            }

            void Init(CargoShip cargoShip)
            {
                this.cargoShip = cargoShip;
                cargoShip.CancelInvoke(cargoShip.BuildingCheck);
                cargoShip.InvokeRepeating(CustomBuildingCheck, 1f, 5f);
            }

            void CustomBuildingCheck()
            {
                List<BaseEntity> entities = Pool.Get<List<BaseEntity>>();
                Vis.Entities(cargoShip.WorldSpaceBounds(), entities, 2162689);

                for (int i = 0; i < entities.Count; i++)
                {
                    BaseEntity entity = entities[i];
                    if (entity == null)
                        continue;

                    JunkPileWater junkPileWater = entity as JunkPileWater;
                    if (junkPileWater != null)
                    {
                        junkPileWater.SinkAndDestroy();
                        continue;
                    }

                    DecayEntity decayEntity = entity as DecayEntity;
                    if (decayEntity != null)
                    {
                        if (!decayEntity.IsAlive() || !decayEntity.isServer || decayEntity.AllowOnCargoShip)
                            continue;

                        if (decayEntity.HasParent())
                        {
                            Barge barge = Barge.GetBargeByEntity(entity);
                            if (barge != null || decayEntity.parentEntity.Get(serverside: true) == cargoShip)
                            {
                                Pool.FreeUnmanaged(ref entities);
                                return;
                            }
                        }

                        decayEntity.Kill(BaseNetworkable.DestroyMode.Gib);
                    }
                }

                Pool.FreeUnmanaged(ref entities);
            }

            internal static void Unload()
            {
                foreach (CargoShipManager cargoShipManager in cargoShipManagers)
                {
                    if (cargoShipManager != null)
                    {
                        cargoShipManager.cargoShip.CancelInvoke(cargoShipManager.CustomBuildingCheck);
                        Destroy(cargoShipManager);
                    }
                }
            }
        }

        class BargeCaller : FacepunchBehaviour
        {
            RoadFlare roadFlare;
            BasePlayer player;
            BargeConfig bargeConfig;
            bool failed;

            internal static BargeCaller Attach(RoadFlare roadFlare, BasePlayer player, BargeConfig bargeConfig)
            {
                BargeCaller bargeCaller = roadFlare.gameObject.AddComponent<BargeCaller>();
                bargeCaller.Init(roadFlare, player, bargeConfig);
                return bargeCaller;
            }

            void Init(RoadFlare roadFlare, BasePlayer player, BargeConfig bargeConfig)
            {
                this.roadFlare = roadFlare;
                this.player = player;
                this.bargeConfig = bargeConfig;
            }

            void OnCollisionEnter(Collision collision)
            {
                failed = true;
                roadFlare.Kill();
            }

            void OnDestroy()
            {
                if (failed || TerrainMeta.HeightMap.GetHeight(this.transform.position) > -10 || IsAnyBlockedEntityNear())
                {
                    LootManager.GiveItemToPLayer(player, bargeConfig.itemConfig, 1);
                    NotifyManager.SendMessageToPlayer(player, "NotEnoughSpace");
                }
                else
                {
                    CallBarge();
                }
            }

            bool IsAnyBlockedEntityNear()
            {
                HashSet<Collider> colliders = Physics.OverlapSphere(roadFlare.transform.position, 20).ToHashSet();

                foreach (Collider collider in colliders)
                {
                    BaseEntity baseEntity = collider.ToBaseEntity();

                    if (baseEntity == null)
                        continue;

                    if (baseEntity is BaseBoat || baseEntity is PercentFullStorageContainer || baseEntity is JunkPileWater)
                        return true;
                }

                return false;
            }

            void CallBarge()
            {
                Barge barge = Barge.SpawnBarge(roadFlare.transform.position + Vector3.up * 20, Quaternion.identity, bargeConfig.presetName, false);
            }
        }

        class DecayManager : FacepunchBehaviour
        {
            BaseEntity parentEntity;
            Coroutine decayCorountine;

            internal static DecayManager AttachDecayManager(BaseEntity parentEntity)
            {
                GameObject triggerGameObject = new GameObject("DecayManager");
                triggerGameObject.transform.SetParent(parentEntity.transform, false);
                DecayManager decayManager = triggerGameObject.AddComponent<DecayManager>();
                decayManager.Init(parentEntity);
                return decayManager;
            }

            void Init(BaseEntity parentEntity)
            {
                this.parentEntity = parentEntity;
                decayCorountine = ServerMgr.Instance.StartCoroutine(DecayCorountine());
            }

            IEnumerator DecayCorountine()
            {
                while (true)
                {
                    for (int i = 0; i < parentEntity.children.Count; i++)
                    {
                        BaseEntity baseEntity = parentEntity.children[i];

                        DecayEntity decayEntity = baseEntity as DecayEntity;
                        if (decayEntity != null)
                            OnDecay(decayEntity);
                    }

                    yield return CoroutineEx.waitForSeconds(600f);
                }
            }

            public virtual void OnDecay(DecayEntity decayEntity)
            {
                BuildingBlock buildingBlock = decayEntity as BuildingBlock;

                if (buildingBlock != null && buildingBlock.grounded)
                    return;

                if (decayEntity.decay == null || ConVar.Decay.scale == 0)
                    return;

                float decayTickOverride = decayEntity.decay.GetDecayTickOverride();

                if (decayTickOverride == 0f)
                    decayTickOverride = ConVar.Decay.tick;

                float timeScienceLastDecay = UnityEngine.Time.time - decayEntity.lastDecayTick;

                decayEntity.lastDecayTick = UnityEngine.Time.time;

                if (!decayEntity.decay.ShouldDecay(decayEntity))
                    return;

                float single = timeScienceLastDecay * ConVar.Decay.scale;

                if (ConVar.Decay.upkeep)
                {
                    decayEntity.upkeepTimer += single;

                    if (decayEntity.upkeepTimer > 0f)
                    {
                        BuildingPrivlidge buildingPrivilege = decayEntity.GetBuildingPrivilege();

                        if (buildingPrivilege != null)
                            decayEntity.upkeepTimer -= buildingPrivilege.PurchaseUpkeepTime(decayEntity, Mathf.Max(decayEntity.upkeepTimer, 600f));
                    }

                    if (decayEntity.upkeepTimer < 1f)
                    {
                        if (decayEntity.healthFraction < 1f && decayEntity.GetEntityHealScale() > 0f && decayEntity.SecondsSinceAttacked > 600f)
                        {
                            if (Interface.CallHook("OnDecayHeal", this) != null)
                                return;

                            float single1 = timeScienceLastDecay / decayEntity.GetEntityDecayDuration() * decayEntity.GetEntityHealScale();
                            decayEntity.Heal(decayEntity.MaxHealth() * single1);
                        }

                        return;
                    }

                    decayEntity.upkeepTimer = 1f;
                }

                decayEntity.decayTimer += single;

                if (decayEntity.decayTimer < decayEntity.GetEntityDecayDelay())
                    return;

                using (TimeWarning timeWarning = TimeWarning.New("DecayTick", 0))
                {
                    float upkeepInsideDecayScale = 1f;
                    if (!ConVar.Decay.upkeep)
                    {
                        for (int i = 0; i < (int)decayEntity.decayPoints.Length; i++)
                        {
                            DecayPoint decayPoint = decayEntity.decayPoints[i];

                            if (decayPoint.IsOccupied(decayEntity))
                                upkeepInsideDecayScale -= decayPoint.protection;
                        }
                    }
                    else if (!decayEntity.BypassInsideDecayMultiplier && !decayEntity.IsOutside())
                    {
                        upkeepInsideDecayScale *= ConVar.Decay.upkeep_inside_decay_scale;
                    }

                    if (Interface.CallHook("OnDecayDamage", this) != null)
                        return;

                    if (upkeepInsideDecayScale > 0f)
                    {
                        float entityDecayDuration = single / decayEntity.GetEntityDecayDuration() * decayEntity.MaxHealth();
                        decayEntity.Hurt(entityDecayDuration * upkeepInsideDecayScale * decayEntity.decayVariance, DamageType.Decay, null, true);
                    }
                }
            }

            internal void DestroyUpdater()
            {
                StopCorountines();
                Destroy(this.gameObject);
            }

            void StopCorountines()
            {
                if (decayCorountine != null)
                    ServerMgr.Instance.StopCoroutine(decayCorountine);
            }
        }


        class Barge : FacepunchBehaviour
        {
            internal static HashSet<Barge> barges = new HashSet<Barge>();

            internal BargeConfig bargeConfig;
            internal BaseEntity mainEntity;
            BuildingVisibilityManager buildingVisibilityUpdater;
            DecayManager decayManager;
            Coroutine bargeUpdateCorountine;
            internal bool shoudUpdateStability;
            internal bool shoudKill;
            internal bool shoudAnchor;
            bool isServerSpawn;
            float lastMovingTime;

            BargePhysics physics;
            HashSet<BaseModule> modules = new HashSet<BaseModule>();
            CustomTriggerParent triggerParent;
            Vector3 colliderSize;
            Vector3 bargeSize;
            BuildingPrivlidge buildingPrivlidge;

            internal static void SaveBarges(bool isUnloading = false)
            {
                if (isUnloading && ins.bargeSaveDatas.Count == 0)
                    return;

                ins.bargeSaveDatas.Clear();

                foreach (Barge barge in barges)
                {
                    if (barge == null || !barge.mainEntity.IsExists() || barge.mainEntity.net == null)
                        continue;
                    int fuelAmount = 0;
                    CabineModule cabineModule = barge.modules.FirstOrDefault(x => x != null && x is CabineModule) as CabineModule;
                    if (cabineModule != null)
                    {
                        fuelAmount = cabineModule.CalculateFuelAmount();
                    }

                    BargeSaveData bargeSaveData = new BargeSaveData
                    {
                        netId = barge.mainEntity.net.ID.Value,
                        bargePreset = barge.bargeConfig.presetName,
                        isServerSpawn = barge.isServerSpawn,
                        fuelAmount = fuelAmount,
                    };

                    ins.bargeSaveDatas.Add(bargeSaveData);
                }

                ins.SaveDataFile(ins.bargeSaveDatas, "save");
            }

            internal static void LoadBarges()
            {
                HashSet<SkyLantern> entities = BaseNetworkable.serverEntities.OfType<SkyLantern>();

                foreach (SkyLantern entity in entities)
                {
                    if (entity == null || entity.net == null)
                        continue;

                    BargeSaveData bargeSaveData = ins.bargeSaveDatas.FirstOrDefault(x => x.netId == entity.net.ID.Value);

                    if (bargeSaveData == null)
                    {
                        if (entity.skinID == 0)
                            continue;

                        BargeConfig bargeConfig = ins._config.bargeConfigs.FirstOrDefault(x => x.itemConfig.skin == entity.skinID);
                        if (bargeConfig != null)
                        {
                            Barge barge1 = TryAttachBargeClass(entity, bargeConfig.presetName, false, false);

                            if (barge1 != null)
                                barge1.buildingVisibilityUpdater.OnStopMoving();
                        }

                        continue;
                    }

                    Barge barge = TryAttachBargeClass(entity, bargeSaveData.bargePreset, false, bargeSaveData.isServerSpawn);

                    if (barge != null)
                        barge.buildingVisibilityUpdater.OnStopMoving();
                }
            }

            internal static void UnloadBarges()
            {
                foreach (Barge barge in barges)
                    if (barge != null)
                        barge.UnloadBarge();
            }

            internal static int GetBargePopulation(bool onlyServerSpawn)
            {
                barges.RemoveWhere(x => x == null);

                if (onlyServerSpawn)
                    return barges.Where(x => x.isServerSpawn).Count;

                return barges.Count;
            }

            internal static void KillAllBarges()
            {
                foreach (Barge barge in barges)
                    if (barge != null && barge.mainEntity.IsExists())
                        barge.KillBarge();

                SaveBarges();
            }

            internal static Barge GetBargeByParentEntityNetId(ulong netId)
            {
                return barges.FirstOrDefault(x => x != null && x.mainEntity.IsExists() && x.mainEntity.net != null && x.mainEntity.net.ID.Value == netId);
            }

            internal static Barge GetBargeByEntity(BaseEntity entity)
            {
                if (entity.ShortPrefabName == "electricfurnace.io" || entity is IndustrialStorageAdaptor)
                {
                    BaseEntity parentEntity = entity.GetParentEntity();

                    if (parentEntity != null)
                        entity = parentEntity;
                }

                return barges.FirstOrDefault(x => x != null && x.mainEntity.IsExists() && x.IsBargeEntity(entity));
            }

            internal static Barge GetBargeByCollider(BaseEntity entity)
            {
                int allowedTopologies = (int)(TerrainTopology.Enum.Ocean | TerrainTopology.Enum.Oceanside);
                TerrainMeta.TopologyMap.GetTopology(entity.transform.position);
                if ((TerrainMeta.TopologyMap.GetTopology(entity.transform.position) & allowedTopologies) == 0)
                    return null;

                return barges.FirstOrDefault(x => x != null && x.IsInsideBargeCollider(entity));
            }

            internal static Barge GetBargeByPhysicsEntityNetId(ulong netId)
            {
                return barges.FirstOrDefault(x => x != null && x.physics.IsMyKayak(netId));
            }

            internal static Barge SpawnBarge(Vector3 position, Quaternion rotation, string presetName, bool isServerSpawn)
            {
                BargeConfig bargeConfig = ins._config.bargeConfigs.FirstOrDefault(x => x.presetName == presetName);
                if (bargeConfig == null)
                {
                    NotifyManager.PrintError(null, "ConfigNotFound_Exeption", presetName);
                    return null;
                }

                BaseEntity mainEntity = BuildManager.SpawnRegularEntity("assets/prefabs/misc/chinesenewyear/sky_lantern/skylantern.deployed.prefab", position, rotation, bargeConfig.itemConfig.skin, true);
                Barge barge = TryAttachBargeClass(mainEntity, presetName, true, isServerSpawn);

                if (barge == null)
                    mainEntity.Kill();

                SaveBarges();
                return barge;
            }

            static Barge TryAttachBargeClass(BaseEntity mainEntity, string presetName, bool firstSpawn, bool isServerSpawn)
            {
                BargeConfig bargeConfig = ins._config.bargeConfigs.FirstOrDefault(x => x.presetName == presetName);

                if (bargeConfig == null)
                {
                    NotifyManager.PrintError(null, "ConfigNotFound_Exeption", presetName);
                    return null;
                }

                BargeCustomizeData bargeData;
                ins.bargeCustomizationDatas.TryGetValue(bargeConfig.dataFileName, out bargeData);

                if (bargeData == null)
                {
                    NotifyManager.PrintError(null, "DataFileNotFound_Exeption", bargeConfig.dataFileName);
                    return null;
                }

                Barge barge = mainEntity.gameObject.AddComponent<Barge>();
                barges.Add(barge);
                barge.BuildBarge(mainEntity, bargeConfig, bargeData, firstSpawn, isServerSpawn);
                return barge;
            }

            void BuildBarge(BaseEntity parentEntity, BargeConfig bargeConfig, BargeCustomizeData bargeData, bool firstSpawn, bool isServerSpawn)
            {
                this.mainEntity = parentEntity;
                this.bargeConfig = bargeConfig;
                this.isServerSpawn = isServerSpawn;

                UpdateMainEntity();

                HashSet<BoxColliderInfo> boxColliserInfos = new HashSet<BoxColliderInfo>();

                foreach (BoxColliderData boxColliderData in bargeData.boxCollisersData)
                {
                    BoxColliderInfo boxColliderInfo = new BoxColliderInfo(boxColliderData);
                    boxColliserInfos.Add(boxColliderInfo);
                }

                SpawnPrefab(bargeData, parentEntity, firstSpawn);

                HashSet<BargeModuleConfig> enabledModules = bargeConfig.modules.Where(x => x.isEnable);
                foreach (DoorCloser doorCloser in mainEntity.children.OfType<DoorCloser>())
                {
                    if (!doorCloser.IsExists())
                        continue;

                    if (enabledModules.Count == 0)
                    {
                        doorCloser.Kill();
                        continue;
                    }
                    BargeModuleConfig bargeModuleConfig = enabledModules.Min(x => Vector3.Distance(doorCloser.transform.localPosition, x.position.ToVector3()));

                    if (bargeModuleConfig == null || Vector3.Distance(doorCloser.transform.localPosition, bargeModuleConfig.position.ToVector3()) > 0.1f)
                        doorCloser.Kill();
                }

                foreach (BargeModuleConfig presetLocationConfig in bargeConfig.modules)
                    SpawnModule(presetLocationConfig, firstSpawn, ref boxColliserInfos);

                ConnectorModule connectorModule = modules.FirstOrDefault(x => x is ConnectorModule) as ConnectorModule;

                physics = this.gameObject.AddComponent<BargePhysics>();
                physics.Init(this, bargeData, boxColliserInfos, connectorModule);

                colliderSize = bargeData.size.ToVector3();

                HashSet<BaseEntity> surroundingTanks = parentEntity.children.Where(x => x != null && x.ShortPrefabName == "coaling_tower_fuel_storage.entity");
                if (surroundingTanks.Count > 0)
                {
                    BaseEntity maxXEntity = surroundingTanks.Max(x => Math.Abs(x.transform.localPosition.x));
                    BaseEntity maxZEntity = surroundingTanks.Max(x => Math.Abs(x.transform.localPosition.z));

                    if (maxXEntity != null && maxZEntity != null)
                        bargeSize = new Vector3(Math.Abs(maxXEntity.transform.localPosition.x), 0, Math.Abs(maxZEntity.transform.localPosition.z));
                }

                bargeUpdateCorountine = ServerMgr.Instance.StartCoroutine(BargeUpdateCorountine());
                buildingVisibilityUpdater = BuildingVisibilityManager.AttachVisibilityUpdater(parentEntity);
                decayManager = DecayManager.AttachDecayManager(parentEntity);
                mainEntity.SetFlag(BaseEntity.Flags.Broken, true);

                if (!firstSpawn)
                {
                    buildingPrivlidge = parentEntity.children.FirstOrDefault(x => x.IsExists() && x is BuildingPrivlidge) as BuildingPrivlidge;
                }

                if (firstSpawn && !isServerSpawn)
                    StartMoving();
            }

            void UpdateMainEntity()
            {
                BuildManager.DestroyEntityConponent<GroundWatch>(mainEntity);
                mainEntity.enableSaving = true;
                mainEntity.skinID = bargeConfig.itemConfig.skin;

                SkyLantern skyLantern = mainEntity as SkyLantern;
                if (skyLantern != null)
                {
                    skyLantern.CancelInvoke(skyLantern.StartSinking);
                    skyLantern.CancelInvoke(skyLantern.SelfDestroy);
                }
            }

            void SpawnModule(BargeModuleConfig presetLocationConfig, bool firstSpawn, ref HashSet<BoxColliderInfo> boxColliserInfos)
            {
                if (!presetLocationConfig.isEnable)
                    return;

                CustomPrefabData customPrefabData;

                if (!ins.moduleCustomizationDatas.TryGetValue(presetLocationConfig.presetName, out customPrefabData))
                {
                    NotifyManager.PrintError(null, "DataFileNotFound_Exeption", presetLocationConfig.presetName);
                    return;
                }

                BaseEntity parentEntity = null;
                parentEntity = mainEntity.children.FirstOrDefault(x => x != null && x.PrefabName == "assets/prefabs/misc/doorcloser/doorcloser.prefab" && Vector3.Distance(x.transform.localPosition, presetLocationConfig.position.ToVector3()) < 0.1f);

                if (parentEntity == null)
                {
                    parentEntity = BuildManager.SpawnChildEntity(mainEntity, "assets/prefabs/misc/doorcloser/doorcloser.prefab", presetLocationConfig.position.ToVector3(), presetLocationConfig.rotation.ToVector3(), 0, false, true);
                }
                parentEntity.enableSaving = true;

                HelicopterDebris helicopterDebris = parentEntity as HelicopterDebris;
                if (helicopterDebris != null)
                {
                    helicopterDebris.CancelInvoke(helicopterDebris.RemoveMe);
                }

                BaseModule baseModule = null;

                foreach (BoxColliderData boxColliderData in customPrefabData.boxCollisersData)
                {
                    Vector3 relativeLocalPosition = boxColliderData.localPosition.ToVector3();
                    Vector3 relativeLocalRotation = boxColliderData.localRotation.ToVector3();

                    Vector3 globalPosition = parentEntity.transform.TransformPoint(relativeLocalPosition);
                    Quaternion globalRotaton = parentEntity.transform.rotation * Quaternion.Euler(relativeLocalRotation);

                    Vector3 localPosition = mainEntity.transform.InverseTransformPoint(globalPosition);
                    Vector3 localRotation = (Quaternion.Inverse(mainEntity.transform.rotation) * globalRotaton).eulerAngles;

                    BoxColliderInfo boxColliderInfo = new BoxColliderInfo(localPosition, localRotation, boxColliderData.size.ToVector3());
                    boxColliserInfos.Add(boxColliderInfo);
                }

                SpawnPrefab(customPrefabData, parentEntity, firstSpawn);

                if (presetLocationConfig.presetName.Contains("connector") && !modules.Any(x => x is ConnectorModule))
                    baseModule = ConnectorModule.SpawnConnectorModule(parentEntity, this, customPrefabData);
                else if (presetLocationConfig.presetName.Contains("ramp"))
                    baseModule = RampModule.SpawnRampModule(parentEntity, this, customPrefabData);
                else if (presetLocationConfig.presetName.Contains("cabine"))
                    baseModule = CabineModule.SpawnCabineModule(parentEntity, this, customPrefabData);
                else if (presetLocationConfig.presetName.Contains("anchor"))
                    baseModule = AnchorModule.SpawnAnchorModule(parentEntity, this, customPrefabData);
                else if (presetLocationConfig.presetName.Contains("dock"))
                    baseModule = DockModule.SpawnDockModule(parentEntity, this, customPrefabData);

                if (baseModule != null)
                    modules.Add(baseModule);
                else if (parentEntity.IsExists())
                    parentEntity.Kill();
            }

            void SpawnPrefab(CustomPrefabData customPrefabData, BaseEntity parentEntity, bool firstSpawn)
            {
                bool isSpawnForPlatform = customPrefabData is BargeCustomizeData;
                uint buildingId = BuildingManager.server.NewBuildingID();

                foreach (EntityData decorEntityData in customPrefabData.decorEntities)
                {
                    Vector3 localPosition = decorEntityData.position.ToVector3();
                    Vector3 localRotation = decorEntityData.rotation.ToVector3();

                    if (!firstSpawn && parentEntity.children.Any(x => x != null && x.PrefabName == decorEntityData.prefabName && x.transform.localPosition == localPosition))
                        continue;

                    BaseEntity entity = BuildManager.SpawnChildEntity(parentEntity, decorEntityData.prefabName, localPosition, localRotation, isDecor: true, enableSaving: true);
                    if (entity.ShortPrefabName == "coaling_tower_fuel_storage.entity" || entity.ShortPrefabName == "mailbox.deployed")
                        entity.SetFlag(BaseEntity.Flags.Busy, true);
                }

                foreach (BuildingBlockData buildingBlockData in customPrefabData.buildingBlocks)
                {
                    Vector3 localPosition = buildingBlockData.position.ToVector3();
                    Vector3 localRotation = buildingBlockData.rotation.ToVector3();

                    BuildingBlock thisBuildingBlock = parentEntity.children.FirstOrDefault(x => x != null && x.PrefabName == buildingBlockData.prefabName && x.transform.localPosition == localPosition && x.transform.localEulerAngles == localRotation) as BuildingBlock;
                    if (thisBuildingBlock != null)
                    {
                        thisBuildingBlock.grounded = true;
                        continue;
                    }

                    BuildingBlock buildingBlock = BuildManager.SpawnChildBuildingBlock(buildingBlockData, parentEntity);
                    buildingBlock.grounded = true;
                }

                foreach (EntityData regularEntityData in customPrefabData.regularEntities)
                {
                    Vector3 localPosition = regularEntityData.position.ToVector3();
                    Vector3 localRotation = regularEntityData.rotation.ToVector3();

                    if (!firstSpawn && parentEntity.children.Any(x => x != null && x.PrefabName == regularEntityData.prefabName && x.transform.localPosition == localPosition))
                        continue;

                    BaseEntity baseEntity = BuildManager.SpawnChildEntity(parentEntity, regularEntityData.prefabName, localPosition, localRotation, isDecor: false, enableSaving: true);
                    if (baseEntity is PercentFullStorageContainer)
                        baseEntity.SetFlag(BaseEntity.Flags.Busy, true);
                }
            }

            internal bool IsStopped()
            {
                return !physics.IsPlysicsEnable();
            }

            internal bool IsBargeEntity(BaseEntity entity)
            {
                if (entity == mainEntity)
                    return true;

                BaseEntity parentEntity = entity.GetParentEntity();

                if (parentEntity == null || parentEntity.net == null)
                    return false;

                if (parentEntity.net.ID.Value == mainEntity.net.ID.Value)
                    return true;

                if (modules.Any(x => x != null && x.IsModuleEntity(entity)))
                    return true;

                return false;
            }

            internal bool IsPlayerCanInterract(BasePlayer player, bool sendMessage)
            {
                if (buildingPrivlidge.IsExists() && !buildingPrivlidge.IsAuthed(player))
                {
                    if (sendMessage)
                        NotifyManager.SendMessageToPlayer(player, "NotAuthorized");

                    return false;
                }

                return true;
            }

            internal bool IsPlayerCanBuild(BasePlayer player, bool sendMessage)
            {
                if (!IsStopped())
                {
                    if (sendMessage)
                        NotifyManager.SendMessageToPlayer(player, "AnchorBarge");

                    return false;
                }

                Vector3 bargeRotation = mainEntity.transform.rotation.eulerAngles;

                if (bargeRotation.x != 0 || bargeRotation.z != 0)
                {
                    if (sendMessage)
                    {
                        if (IsBargeOnShoal())
                            NotifyManager.SendMessageToPlayer(player, "WrongPositionOnShole");
                        else
                            NotifyManager.SendMessageToPlayer(player, "WrongPosition");
                    }

                    return false;
                }

                return true;
            }

            internal bool IsBasicBuildingBlock(BuildingBlock buildingBlock)
            {
                return buildingBlock.grounded && buildingBlock.OwnerID == 0;
            }

            internal void OnPlayerBuild(BasePlayer player, BaseEntity entity, BaseEntity targetEntity)
            {
                if (entity == null)
                    return;

                if (!IsEntityShoudParrent(entity))
                    return;

                BuildingBlock buildingBlock = entity as BuildingBlock;
                if (buildingBlock != null)
                {
                    entity.SetParent(mainEntity, true, true);
                    BuildingBlock parentBuildingBlock = targetEntity as BuildingBlock;

                    if (IsPlayerCanBuild(player, true) && IsPlayerCanBuild(buildingBlock, player))
                    {
                        shoudUpdateStability = true;
                    }
                    else
                    {
                        buildingBlock.Invoke(() => buildingBlock.Kill(BaseNetworkable.DestroyMode.Gib), 0.1f);
                    }

                    return;
                }

                if (IsEntityShoutParentToTargetEntity(entity))
                    entity.SetParent(targetEntity, true, true);
                else
                    entity.SetParent(mainEntity, true, true);

                BuildingPrivlidge newbuildingPrivlidge = entity as BuildingPrivlidge;
                if (newbuildingPrivlidge != null)
                {
                    if (buildingPrivlidge.IsExists())
                    {
                        newbuildingPrivlidge.Kill();
                        return;
                    }

                    buildingPrivlidge = newbuildingPrivlidge;
                }
            }

            internal bool IsEntityShoudParrent(BaseEntity baseEntity)
            {
                if (baseEntity is BaseLock or DoorCloser or DoorKnocker or GrowableEntity or IndustrialStorageAdaptor or StorageMonitor or IndustrialCrafter or TorchDeployableLightSource or TorchWeapon)
                    return false;

                return true;
            }

            internal bool IsEntityShoutParentToTargetEntity(BaseEntity entity)
            {
                if (entity is Door or ShopFront)
                    return true;

                if (entity.ShortPrefabName.Contains("wall.window.bars"))
                    return true;

                if (entity.ShortPrefabName.Contains("shutter.metal"))
                    return true;

                if (entity.ShortPrefabName.Contains("wall.frame.fence") || entity.ShortPrefabName.Contains("wall.frame.netting") || entity.ShortPrefabName.Contains("wall.frame.cell"))
                    return true;

                if (entity.ShortPrefabName.Contains("floor.grill") || entity.ShortPrefabName.Contains("floor.triangle.grill"))
                    return true;

                return false;
            }

            bool IsPlayerCanBuild(BuildingBlock buildingBlock, BasePlayer player)
            {
                if (buildingBlock.ShortPrefabName.Contains("floor") && !IsInsideBargeCollider(buildingBlock))
                {
                    if (player != null)
                        NotifyManager.SendMessageToPlayer(player, "OutsideBarge");

                    return false;
                }
                else if (bargeConfig.maxFlors > 0 && !buildingBlock.ShortPrefabName.Contains("floor"))
                {
                    float maxHeigth = (bargeConfig.maxFlors - 1) * 3 + 1.75f + 0.9f;
                    float currentHeigth = buildingBlock.transform.position.y - mainEntity.transform.position.y;

                    if (currentHeigth > maxHeigth)
                    {
                        if (player != null)
                            NotifyManager.SendMessageToPlayer(player, "TooHigh", bargeConfig.maxFlors.ToString());

                        return false;
                    }
                }

                return true;
            }

            internal bool IsInsideBargeCollider(BaseEntity entity)
            {
                Vector3 positionForCheck = new Vector3(entity.transform.position.x, mainEntity.transform.position.y, entity.transform.position.z);

                if (entity is BuildingBlock && entity.PrefabName.Contains("triangle"))
                    positionForCheck += entity.transform.forward * 2;

                if (colliderSize.z == 0)
                {
                    if (Vector3.Distance(positionForCheck, mainEntity.transform.position) > colliderSize.x / 2 - 1)
                    {
                        return false;
                    }
                }
                else
                {
                    Vector3 localCheckPosition = PositionDefiner.GetLocalPosition(mainEntity.transform, positionForCheck);

                    if (Math.Abs(localCheckPosition.x) >= bargeSize.x || Math.Abs(localCheckPosition.z) >= bargeSize.z)
                        return false;
                }

                return true;
            }

            internal BargePhysics GetBargePhysics()
            {
                return physics;
            }

            void SwitchParrentTrigger(bool isEnable)
            {
                if (isEnable)
                {
                    if (triggerParent != null)
                    {
                        triggerParent.enabled = true;
                        return;
                    }

                    GameObject triggerGameObject = new GameObject("TriggerPlayerParrent");
                    triggerGameObject.layer = 18;
                    triggerGameObject.transform.SetParent(mainEntity.transform, false);
                    AttachCollider(triggerGameObject);

                    triggerParent = triggerGameObject.AddComponent<CustomTriggerParent>();
                    triggerParent.entityContents = new HashSet<BaseEntity>();
                    triggerParent.interestLayers = (1 << 0 | 1 << 9 | 1 << 11 | 1 << 15 | 1 << 17 | 1 << 31);
                    triggerParent.doClippingCheck = false;
                    triggerParent.parentMountedPlayers = true;
                    triggerParent.parentSleepers = true;
                    triggerParent.ParentNPCPlayers = false;
                    triggerParent.overrideOtherTriggers = false;
                    triggerParent.checkForObjUnderFeet = false;

                    foreach (BasePlayer player in BasePlayer.allPlayerList)
                        if (player.IsRealPlayer() && player.IsSleeping() && IsInsideBargeCollider(player))
                            triggerParent.OnEntityEnter(player);
                }
                else
                {
                    if (triggerParent == null)
                        return;

                    if (triggerParent.entityContents != null)
                    {
                        List<BaseEntity> content = Pool.Get<List<BaseEntity>>();

                        foreach (BaseEntity entity in triggerParent.entityContents)
                            if (entity != null)
                                content.Add(entity);

                        foreach (BaseEntity entity in content)
                            triggerParent.OnEntityLeave(entity);

                        Pool.FreeUnmanaged(ref content);
                    }

                    UnityEngine.Object.DestroyImmediate(triggerParent.gameObject);
                }
            }

            internal void StartMoving()
            {
                if (!IsStopped())
                {
                    lastMovingTime = UnityEngine.Time.realtimeSinceStartup;
                    return;
                }

                OnStartMovingEntitiesUpdate();
                buildingVisibilityUpdater.OnStartMoving();
                SwitchParrentTrigger(true);
                physics.SwitchPlysics(true);
                lastMovingTime = UnityEngine.Time.realtimeSinceStartup;
                mainEntity.SendNetworkUpdate();
            }

            void OnStartMovingEntitiesUpdate()
            {
                bargeSize.y = float.MinValue;

                for (int i = 0; i < mainEntity.children.Count; i++)
                {
                    BaseEntity baseEntity = mainEntity.children[i];
                    BuildingBlock buildingBlock = baseEntity as BuildingBlock;
                    if (buildingBlock != null)
                    {
                        if (ins._config.performanceConfig.killTwig && buildingBlock.grade == 0)
                        {
                            buildingBlock.Kill();
                            continue;
                        }

                        if (buildingBlock.transform.localPosition.y > bargeSize.y)
                            bargeSize.y = buildingBlock.transform.localPosition.y;

                        continue;
                    }

                    BaseMountable baseMountable = baseEntity as BaseMountable;
                    if (baseMountable != null)
                    {
                        if ((baseMountable is BaseVehicleSeat && baseMountable.ShortPrefabName != "arcadeuser") || baseMountable is MovableBaseMountable)
                            continue;

                        baseMountable.DismountAllPlayers();
                        continue;
                    }

                    ModularCarGarage modularCarGarage = baseEntity as ModularCarGarage;
                    if (modularCarGarage != null)
                    {
                        modularCarGarage.enabled = false;
                        continue;
                    }
                }
            }

            internal void StopMoving(bool isUnloading)
            {
                physics.SwitchPlysics(false);

                if (!IsBargeOnShoal())
                {
                    Vector3 rotation = mainEntity.transform.rotation.eulerAngles;
                    mainEntity.transform.rotation = Quaternion.Euler(new Vector3(0, rotation.y, 0));
                }

                SwitchParrentTrigger(false);

                Invoke((() =>
                {
                    buildingVisibilityUpdater.OnStopMoving();
                    OnStopMovingEntitiesUpdate();

                    foreach (BaseModule baseModule in modules)
                        if (baseModule != null)
                            baseModule.UpdateModuleVisibiliy();
                }), 0.5f);

                foreach (BaseModule baseModule in modules)
                    if (baseModule != null)
                        baseModule.OnBargeStopMoving();
            }

            void OnStopMovingEntitiesUpdate()
            {
                for (int i = 0; i < mainEntity.children.Count; i++)
                {
                    BaseEntity entity = mainEntity.children[i];

                    ModularCarGarage modularCarGarage = entity as ModularCarGarage;
                    if (modularCarGarage != null)
                    {
                        Transform snapLocation = modularCarGarage.magnetSnap.GetPrivateFieldValue("snapLocation") as Transform;
                        modularCarGarage.magnetSnap.SetPrivateFieldValue("prevSnapLocation", snapLocation.position);
                        modularCarGarage.enabled = true;
                        continue;
                    }
                }
            }

            Collider AttachCollider(GameObject gameObject)
            {
                float colliderHeigth = bargeSize.y + 7.5f;

                if (colliderSize.z == 0)
                {
                    CapsuleCollider capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
                    capsuleCollider.isTrigger = true;
                    capsuleCollider.center = new Vector3(0, 0.8f + colliderHeigth / 2, 0);
                    capsuleCollider.radius = colliderSize.x / 2;
                    capsuleCollider.height = colliderHeigth;
                    return capsuleCollider;
                }
                else
                {
                    BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
                    boxCollider.isTrigger = true;
                    boxCollider.size = new Vector3(colliderSize.x, colliderHeigth, colliderSize.z);
                    boxCollider.center = new Vector3(0, 0.8f + colliderHeigth / 2, 0);
                    return boxCollider;
                }
            }

            internal bool IsBargeOnShoal(float maxYPos = -3.5f)
            {
                if (IsOnShole(transform.position, maxYPos))
                    return true;

                Vector3 test1 = PositionDefiner.GetGlobalPosition(mainEntity.transform, new Vector3(bargeSize.x, 0, bargeSize.z));
                Vector3 test2 = PositionDefiner.GetGlobalPosition(mainEntity.transform, new Vector3(bargeSize.x, 0, -bargeSize.z));
                Vector3 test3 = PositionDefiner.GetGlobalPosition(mainEntity.transform, new Vector3(-bargeSize.x, 0, bargeSize.z));
                Vector3 test4 = PositionDefiner.GetGlobalPosition(mainEntity.transform, new Vector3(-bargeSize.x, 0, -bargeSize.z));

                return IsOnShole(test1, maxYPos) || IsOnShole(test2, maxYPos) || IsOnShole(test3, maxYPos) || IsOnShole(test4, maxYPos);
            }

            bool IsOnShole(Vector3 position, float maxYPos)
            {
                return TerrainMeta.HeightMap.GetHeight(position) > maxYPos;
            }

            void FixedUpdate()
            {
                if (shoudAnchor)
                {
                    if (IsBargeOnShoal(0) || (Math.Abs(mainEntity.transform.position.y) <= 0.5f && Vector3.Angle(mainEntity.transform.forward, new Vector3(mainEntity.transform.forward.x, 0, mainEntity.transform.forward.z)) < 2.5f && Vector3.Angle(mainEntity.transform.right, new Vector3(mainEntity.transform.right.x, 0, mainEntity.transform.right.z)) < 3.5f))
                    {
                        shoudAnchor = false;
                        StopMoving(false);
                    }
                }
            }

            IEnumerator BargeUpdateCorountine()
            {
                while (true)
                {
                    if (shoudUpdateStability)
                    {
                        StabilityManager.UpdateChildEntitiesStability(mainEntity);
                        shoudUpdateStability = false;
                    }

                    if (!IsStopped() && !shoudAnchor)
                    {
                        foreach (BaseModule baseModule in modules)
                        {
                            if (baseModule == null)
                                continue;

                            if (baseModule.IsModuleMoving())
                            {
                                lastMovingTime = UnityEngine.Time.realtimeSinceStartup;
                                break;
                            }
                        }

                        if (UnityEngine.Time.realtimeSinceStartup - lastMovingTime > 30f)
                            shoudAnchor = true;
                    }
                    yield return CoroutineEx.waitForSeconds(0.25f);

                    if (!IsStopped())
                        buildingVisibilityUpdater.UpdateBuildingBlockModels();

                    yield return CoroutineEx.waitForSeconds(2f);
                }
            }

            void UnloadBarge()
            {
                StopMoving(true);
                physics.DestroyPhysics();

                foreach (BaseModule baseModule in modules)
                    if (baseModule != null)
                        baseModule.UnloadModule();

                if (triggerParent != null)
                    UnityEngine.GameObject.Destroy(triggerParent.gameObject);

                if (buildingVisibilityUpdater != null)
                    buildingVisibilityUpdater.DestroyUpdater();

                if (decayManager != null)
                    decayManager.DestroyUpdater();

                Destroy(this);
            }

            internal void KillBarge()
            {
                shoudKill = true;

                if (mainEntity.IsExists())
                    mainEntity.Kill();

                physics.DestroyPhysics();
            }

            void OnDestroy()
            {
                if (bargeUpdateCorountine != null)
                    ServerMgr.Instance.StopCoroutine(bargeUpdateCorountine);
            }
        }

        class CustomTriggerParent : TriggerParentEnclosed
        {
            public override bool ShouldParent(BaseEntity ent, bool bypassOtherTriggerCheck = false)
            {
                if (ent != null && ent.ShortPrefabName.Contains("kayak"))
                    return false;

                return base.ShouldParent(ent, bypassOtherTriggerCheck);
            }
        }

        class BargePhysics : FacepunchBehaviour
        {
            Barge barge;
            Rigidbody rigidbody;
            Buoyancy buoyancy;
            List<string> buoyancyPoints;
            BaseEntity fakePhysicsEntity;
            BaseMountable driverMountable;
            HashSet<BoxColliderInfo> boxColliserInfos = new HashSet<BoxColliderInfo>();
            ConnectorModule connectorModule;
            float lastBadZoneEntertime;

            internal static BargePhysics AddPhysics(Barge barge, BargeCustomizeData bargeData, HashSet<BoxColliderInfo> boxColliserInfos, ConnectorModule connectorModule)
            {
                BargePhysics bargePhysics = barge.gameObject.AddComponent<BargePhysics>();
                bargePhysics.Init(barge, bargeData, boxColliserInfos, connectorModule);
                return bargePhysics;
            }

            internal void Init(Barge barge, BargeCustomizeData bargeData, HashSet<BoxColliderInfo> boxColliserInfos, ConnectorModule connectorModule)
            {
                this.barge = barge;
                this.boxColliserInfos = boxColliserInfos;
                this.connectorModule = connectorModule;
                buoyancyPoints = bargeData.buoyancyPoints;
            }

            internal void SwitchPlysics(bool isPlysicsEnable)
            {
                if (isPlysicsEnable)
                {
                    if (fakePhysicsEntity.IsExists())
                        return;

                    CreateFakePhysicEntity();
                    CreateFakeEntityColliders();
                }
                else
                {
                    if (fakePhysicsEntity.IsExists())
                        fakePhysicsEntity.Kill();
                }
            }

            internal bool IsPhysicsEnable()
            {
                return fakePhysicsEntity.IsExists();
            }

            internal int GetSpeed()
            {
                if (!IsPhysicsEnable())
                    return 0;

                return (int)rigidbody.velocity.magnitude;
            }

            void CreateFakePhysicEntity()
            {
                Kayak kayak = GameManager.server.CreateEntity("assets/content/vehicles/boats/kayak/kayak.prefab", barge.transform.position, barge.transform.rotation) as Kayak;
                kayak.enableSaving = false;
                BaseEntity baseVehicle = kayak.gameObject.AddComponent<BaseEntity>();
                BuildManager.CopySerializableFields(kayak, baseVehicle);
                kayak.StopAllCoroutines();
                UnityEngine.GameObject.DestroyImmediate(kayak, true);
                baseVehicle.SetFlag(BaseEntity.Flags.On, true);
                fakePhysicsEntity = baseVehicle;

                fakePhysicsEntity.Spawn();
                BuildManager.DestroyEntityConponents<EntityCollisionMessage>(fakePhysicsEntity);
                BuildManager.DestroyEntityConponents<BuoyancyPoint>(fakePhysicsEntity);
                BuildManager.DestroyEntityConponent<TriggerPlayerTimer>(fakePhysicsEntity);
                BuildManager.DestroyEntityConponent<TriggerNotify>(fakePhysicsEntity);

                rigidbody = fakePhysicsEntity.GetComponentInChildren<Rigidbody>();
                rigidbody.centerOfMass = new Vector3(0, -10f, 0);
                rigidbody.mass = barge.bargeConfig.mass;
                rigidbody.drag = 1;
                rigidbody.angularDrag = 1;
                rigidbody.sleepThreshold = 0;
                rigidbody.isKinematic = true;

                buoyancy = fakePhysicsEntity.gameObject.GetComponentInChildren<Buoyancy>();
                buoyancy.requiredSubmergedFraction = 0.5f;
                buoyancy.wavesEffect = 0.6f;
                buoyancy.points = new BuoyancyPoint[buoyancyPoints.Count];
                buoyancy.SetPrivateFieldValue("timeInWater", 0f);
                buoyancy.InvokeRepeating(() => buoyancy.SetPrivateFieldValue("timeInWater", 1f), 1f, 4f);

                for (int i = 0; i < buoyancyPoints.Count; i++)
                {
                    GameObject gameObject = new GameObject("BuoyancyPoint");
                    gameObject.transform.parent = fakePhysicsEntity.gameObject.transform;
                    gameObject.transform.localPosition = buoyancyPoints[i].ToVector3();
                    BuoyancyPoint buoyancyPoint = gameObject.AddComponent<BuoyancyPoint>();
                    buoyancyPoint.buoyancyForce = (rigidbody.mass / (buoyancyPoints.Count / 2) * -Physics.gravity.y);
                    buoyancyPoint.waveFrequency = 0.5f;
                    buoyancyPoint.waveScale = 1;
                    buoyancyPoint.size = 1;
                    buoyancy.points[i] = buoyancyPoint;
                }

                buoyancy.SavePointData(true);
                CreateFakeEntityColliders();

                fakePhysicsEntity.Invoke(() =>
                {
                    CollisionDisabler.AttachCollisonDisabler(fakePhysicsEntity, barge);
                    rigidbody.isKinematic = false;
                    rigidbody.WakeUp();
                    buoyancy.SetPrivateFieldValue("timeInWater", 1f);
                    buoyancy.Wake();

                    if (barge.IsBargeOnShoal())
                    {
                        fakePhysicsEntity.transform.position += Vector3.up * 1.25f;
                    }

                }, 1f);
            }

            void CreateFakeEntityColliders()
            {
                if (boxColliserInfos.Any(x => x.localRotation != Vector3.zero))
                {
                    foreach (BoxColliderInfo boxColliderInfo in boxColliserInfos)
                    {
                        GameObject gameObject = new GameObject("BuildingBlockCustomCollider");
                        gameObject.transform.localPosition = boxColliderInfo.localPosition;
                        gameObject.transform.localEulerAngles = boxColliderInfo.localRotation;
                        gameObject.transform.SetParent(fakePhysicsEntity.transform, false);
                        gameObject.layer = 9;

                        BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
                        boxCollider.size = boxColliderInfo.size;
                        boxCollider.center = Vector3.zero;
                        boxCollider.material.dynamicFriction = 0f;
                        boxCollider.material.staticFriction = 0f;
                    }
                }
                else
                {
                    GameObject gameObject = new GameObject("BuildingBlockCustomCollider");
                    gameObject.transform.SetParent(fakePhysicsEntity.transform, false);
                    gameObject.layer = 9;

                    foreach (BoxColliderInfo boxColliderInfo in boxColliserInfos)
                    {
                        BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
                        boxCollider.size = boxColliderInfo.size;
                        boxCollider.center = boxColliderInfo.localPosition;
                        boxCollider.material.dynamicFriction = 0;
                        boxCollider.material.staticFriction = 0;
                    }
                }
            }

            internal bool IsMyKayak(ulong netID)
            {
                return fakePhysicsEntity != null && fakePhysicsEntity.net != null && fakePhysicsEntity.net.ID.Value == netID;
            }

            internal bool IsPlysicsEnable()
            {
                return fakePhysicsEntity.IsExists();
            }

            void FixedUpdate()
            {
                if (!fakePhysicsEntity.IsExists())
                    return;

                CheckShoreDistance();
                barge.mainEntity.transform.position = fakePhysicsEntity.transform.position;
                barge.mainEntity.transform.rotation = fakePhysicsEntity.transform.rotation;
            }

            void CheckShoreDistance()
            {
                if (ins._config.mainConfig.maxShoreDistance <= 0)
                    return;

                float xDistanceToShore = fakePhysicsEntity.transform.position.x - World.Size / 2;
                float zDistanceToShore = fakePhysicsEntity.transform.position.x - World.Size / 2;
                float distanceToShore = xDistanceToShore > zDistanceToShore ? xDistanceToShore : zDistanceToShore;

                if (distanceToShore > ins._config.mainConfig.maxShoreDistance)
                {
                    if (UnityEngine.Time.realtimeSinceStartup - lastBadZoneEntertime < 5)
                        return;

                    PushBarge(-fakePhysicsEntity.transform.position.normalized);
                }
            }

            internal void OnBargeEnterToBlockZoneZone(Vector3 safeZonePosition)
            {
                Vector3 direction = (fakePhysicsEntity.transform.position - safeZonePosition).normalized;
                PushBarge(direction);

                if (connectorModule != null)
                    connectorModule.BreakConnection();
            }

            internal void PushBarge(Vector3 direction)
            {
                rigidbody.velocity = 10 * direction;
                lastBadZoneEntertime = UnityEngine.Time.realtimeSinceStartup;
            }

            internal void AddForceAtPosition(Vector3 force, Vector3 position)
            {
                if (rigidbody == null)
                    return;

                if (UnityEngine.Time.realtimeSinceStartup - lastBadZoneEntertime < 5)
                    return;

                rigidbody.AddForceAtPosition(force * rigidbody.mass * 5, position);
            }

            internal void OnBoatWantConnect(BaseBoat baseBoat)
            {
                if (connectorModule == null || fakePhysicsEntity == null || connectorModule.springJoint != null || !connectorModule.isActive)
                    return;

                Vector3 boatConnectorLocalPosition = baseBoat is Tugboat ? new Vector3(0, 0, -11.5f) : baseBoat is RHIB ? new Vector3(0, 0.75f, -4.5f) : Vector3.zero;
                Vector3 boatConnectorGlobalPosition = PositionDefiner.GetGlobalPosition(baseBoat.transform, boatConnectorLocalPosition);
                Vector3 connectorLocalPosition = new Vector3(0, 0f, 5.3f);
                Vector3 connectorGlobalPosition = PositionDefiner.GetGlobalPosition(connectorModule.transform, connectorLocalPosition);
                float distanceToConnector = Vector3.Distance(boatConnectorGlobalPosition, connectorGlobalPosition);

                if (distanceToConnector < 2.5f)
                {
                    connectorModule.springJoint = fakePhysicsEntity.gameObject.AddComponent<SpringJoint>();
                    connectorModule.springJoint.connectedBody = baseBoat.rigidBody;
                    connectorModule.springJoint.autoConfigureConnectedAnchor = false;

                    connectorModule.springJoint.breakForce = float.MaxValue;
                    connectorModule.springJoint.breakTorque = float.MaxValue;
                    connectorModule.springJoint.connectedAnchor = boatConnectorLocalPosition;
                    connectorModule.springJoint.anchor = PositionDefiner.GetLocalPosition(fakePhysicsEntity.transform, connectorGlobalPosition);
                    connectorModule.springJoint.enableCollision = true;

                    connectorModule.springJoint.spring = rigidbody.mass * 10;
                    connectorModule.springJoint.minDistance = 0.4f;
                    connectorModule.springJoint.maxDistance = 0.45f;

                    connectorModule.SwitchConnector(false);
                    connectorModule.connectedRigidbody = baseBoat.rigidBody;
                }
            }

            internal void OnBargeWantConnect(BargePhysics bargePhysics)
            {
                if (bargePhysics == this)
                    return;

                if (connectorModule == null || bargePhysics.connectorModule == null)
                    return;

                if (!connectorModule.isActive || !bargePhysics.connectorModule.isActive)
                    return;

                if (connectorModule == null || fakePhysicsEntity == null || connectorModule.springJoint != null)
                    return;

                if (bargePhysics.connectorModule == null || bargePhysics.fakePhysicsEntity == null || bargePhysics.connectorModule.springJoint != null)
                    return;

                if (fakePhysicsEntity.net.ID.Value < bargePhysics.fakePhysicsEntity.net.ID.Value)
                    return;

                Vector3 connectorLocalPosition = new Vector3(0, 0f, 5.3f);
                Vector3 connectorGlobalPosition = PositionDefiner.GetGlobalPosition(connectorModule.transform, connectorLocalPosition);
                Vector3 otherConnectorGlobalPosition = PositionDefiner.GetGlobalPosition(bargePhysics.connectorModule.transform, connectorLocalPosition);
                float distanceToConnector = Vector3.Distance(otherConnectorGlobalPosition, connectorGlobalPosition);

                if (distanceToConnector < 2.5f)
                {
                    SpringJoint springJoint = fakePhysicsEntity.gameObject.AddComponent<SpringJoint>();
                    springJoint.connectedBody = bargePhysics.rigidbody;
                    springJoint.autoConfigureConnectedAnchor = false;

                    springJoint.breakForce = float.MaxValue;
                    springJoint.breakTorque = float.MaxValue;
                    springJoint.connectedAnchor = PositionDefiner.GetLocalPosition(bargePhysics.fakePhysicsEntity.transform, otherConnectorGlobalPosition);
                    springJoint.anchor = PositionDefiner.GetLocalPosition(fakePhysicsEntity.transform, connectorGlobalPosition);
                    springJoint.enableCollision = true;

                    springJoint.spring = rigidbody.mass * 10;
                    springJoint.minDistance = 0.4f;
                    springJoint.maxDistance = 0.45f;

                    connectorModule.SwitchConnector(false);
                    bargePhysics.connectorModule.SwitchConnector(false);
                    connectorModule.connectedRigidbody = bargePhysics.rigidbody;
                    connectorModule.springJoint = springJoint;
                    bargePhysics.connectorModule.connectedRigidbody = rigidbody;
                }
            }

            internal void DestroyPhysics()
            {
                if (fakePhysicsEntity.IsExists())
                    fakePhysicsEntity.Kill();

                UnityEngine.GameObject.Destroy(this);
            }
        }

        class DockModule : BaseModule
        {
            SphereCollider sphereCollider;
            BaseVehicle grabBoat;
            bool isFullyGrab;
            float idealLocalRotation;
            Vector3 idealLocalPosition;
            float lastReleaseTime;

            internal static DockModule SpawnDockModule(BaseEntity parentEntity, Barge barge, CustomPrefabData customPrefabData)
            {
                DockModule dockModule = parentEntity.gameObject.AddComponent<DockModule>();
                dockModule.Init(parentEntity, barge, customPrefabData);
                dockModule.LoadModule();
                return dockModule;
            }

            void LoadModule()
            {
                parentEntity.gameObject.layer = 18;
                sphereCollider = parentEntity.gameObject.AddComponent<SphereCollider>();
                sphereCollider.radius = 3f;
                sphereCollider.center = new Vector3(-2.523f, 0, 0);
                sphereCollider.isTrigger = true;

                BaseVehicle baseVehicle = parentEntity.children.FirstOrDefault(x => x != null && x is BaseVehicle) as BaseVehicle;

                if (baseVehicle != null)
                    TryGrabBoat(baseVehicle, false);
            }

            internal override void OnButtonPressed(BasePlayer player)
            {
                ElectricEffect();

                if (grabBoat != null)
                    ReleaseBoat();
            }

            void ElectricEffect()
            {
                Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.shock.prefab", PositionDefiner.GetGlobalPosition(parentEntity.transform, new Vector3(-1.742f, 0.162f, 0.557f)), broadcast: true);
                Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.shock.prefab", PositionDefiner.GetGlobalPosition(parentEntity.transform, new Vector3(-1.742f, 0.162f, -0.557f)), broadcast: true);
            }

            void OnTriggerEnter(Collider other)
            {
                if (grabBoat != null)
                    return;

                BaseEntity baseEntity = other.ToBaseEntity();

                if (baseEntity == null)
                    return;

                BaseVehicle vehicle = baseEntity as BaseVehicle;

                if (vehicle == null || vehicle.HasParent() || vehicle is Tugboat || !(vehicle is RHIB or MotorRowboat or BaseSubmarine))
                    return;

                if (UnityEngine.Time.realtimeSinceStartup - lastReleaseTime < 10)
                    return;

                TryGrabBoat(vehicle, true);
            }

            void TryGrabBoat(BaseVehicle baseVehicle, bool checkPlayers)
            {
                if (checkPlayers)
                {
                    List<BasePlayer> mountedPlayers = Pool.Get<List<BasePlayer>>();
                    baseVehicle.GetMountedPlayers(mountedPlayers);

                    if (mountedPlayers.Count > 0 && !mountedPlayers.Any(x => x != null && barge.IsPlayerCanInterract(x, false)))
                    {
                        Pool.FreeUnmanaged(ref mountedPlayers);
                        return;
                    }

                    Pool.FreeUnmanaged(ref mountedPlayers);
                }

                ElectricEffect();
                grabBoat = baseVehicle;
                idealLocalRotation = Vector3.Angle(parentEntity.transform.forward, grabBoat.transform.forward) <= 90 ? 0 : 180;
                idealLocalPosition = grabBoat is RHIB ? new Vector3(-2.731f, -1.115f, 0) : grabBoat is MotorRowboat ? new Vector3(-2.7f, -0.453f, 0) : grabBoat is SubmarineDuo ? new Vector3(-2.498f, -0.345f, 0) : new Vector3(-2.529f, -0.526f, 0);
                grabBoat.SetToKinematic();
                grabBoat.SetParent(parentEntity, true);
                isFullyGrab = false;
            }

            internal void ReleaseBoat()
            {
                grabBoat.SetParent(null, true);
                grabBoat.SetToNonKinematic();
                grabBoat.rigidBody.detectCollisions = true;
                grabBoat = null;
                lastReleaseTime = UnityEngine.Time.realtimeSinceStartup;
                isFullyGrab = false;
            }

            void FixedUpdate()
            {
                if (!isFullyGrab && grabBoat != null && grabBoat.HasParent())
                {
                    Vector3 targetRotation = idealLocalRotation == 0 ? new Vector3(0, (grabBoat.transform.localEulerAngles.y + idealLocalRotation) / 1.05f, 0) : new Vector3(0, 180 + (grabBoat.transform.localEulerAngles.y - 180 + 0) / 1.05f, 0);
                    grabBoat.transform.localPosition = Vector3.Lerp(grabBoat.transform.localPosition, idealLocalPosition, 0.1f);
                    grabBoat.transform.localEulerAngles = targetRotation;

                    if (Vector3.Distance(grabBoat.transform.localPosition, idealLocalPosition) < 0.02f)
                    {
                        isFullyGrab = true;
                        grabBoat.transform.localPosition = idealLocalPosition;
                        grabBoat.transform.localEulerAngles = new Vector3(0, idealLocalRotation, 0);
                    }
                }
            }
        }

        class AnchorModule : BaseModule
        {
            IOEntity sirenLight;

            internal static AnchorModule SpawnAnchorModule(BaseEntity parentEntity, Barge barge, CustomPrefabData customPrefabData)
            {
                AnchorModule anchorModule = parentEntity.gameObject.AddComponent<AnchorModule>();
                anchorModule.Init(parentEntity, barge, customPrefabData);
                anchorModule.LoadModule();

                return anchorModule;
            }

            void LoadModule()
            {
                sirenLight = parentEntity.children.FirstOrDefault(x => x.IsExists() && x is IOEntity && x.ShortPrefabName == "sirenlightorange") as IOEntity;
                pressButton.pressDuration = 2f;
            }

            internal override void OnButtonPressed(BasePlayer player)
            {
                if (barge.IsStopped())
                {
                    Effect.server.Run("assets/bundled/prefabs/fx/oiljack/pump_up.prefab", parentEntity.transform.position + Vector3.up, broadcast: true);
                    parentEntity.Invoke(() => barge.StartMoving(), 1);
                }
                else
                {
                    Effect.server.Run("assets/bundled/prefabs/fx/oiljack/pump_down.prefab", parentEntity.transform.position + Vector3.up, broadcast: true);
                    SwitchSirenLigth(true);

                    parentEntity.Invoke(() =>
                    {
                        SwitchSirenLigth(false);
                        barge.shoudAnchor = true;
                        Effect.server.Run("assets/content/vehicles/submarine/effects/submarine collision effect abovewater.prefab", parentEntity.transform.position - Vector3.up * 2, broadcast: true);
                    }, 1);
                }
            }

            void SwitchSirenLigth(bool isEnable)
            {
                if (sirenLight != null)
                    sirenLight.UpdateFromInput(isEnable ? 1 : 0, 0);
            }
        }

        class ConnectorModule : BaseModule
        {
            BargePhysics physics;
            IOEntity sirenLight;
            HashSet<GameObject> gameObjects = new HashSet<GameObject>();
            ConnectorTrigger connectorTrigger;
            internal bool isActive;
            internal Rigidbody connectedRigidbody;
            internal SpringJoint springJoint;

            internal static ConnectorModule SpawnConnectorModule(BaseEntity parentEntity, Barge barge, CustomPrefabData customPrefabData)
            {
                ConnectorModule connectorModule = parentEntity.gameObject.AddComponent<ConnectorModule>();
                connectorModule.Init(parentEntity, barge, customPrefabData);
                parentEntity.Invoke(() => connectorModule.LoadModule(), 1f);
                return connectorModule;
            }

            protected void LoadModule()
            {
                sirenLight = parentEntity.children.FirstOrDefault(x => x.IsExists() && x is IOEntity && x.ShortPrefabName == "sirenlightorange") as IOEntity;
                sirenLight.SetFlag(BaseEntity.Flags.InUse, true);
                parentEntity.gameObject.layer = (int)Rust.Layer.Reserved1;
                physics = barge.GetBargePhysics();
            }

            internal override void OnButtonPressed(BasePlayer player)
            {
                if (springJoint != null)
                {
                    BreakConnection();
                    return;
                }

                if (isActive)
                    SwitchConnector(false);
                else
                    SwitchConnector(true);
            }

            internal void SwitchConnector(bool isEnable)
            {
                isActive = isEnable;

                if (sirenLight != null)
                    sirenLight.UpdateFromInput(isActive ? 1 : 0, 0);

                if (isEnable)
                {
                    barge.StartMoving();
                    connectorTrigger = ConnectorTrigger.AttachConnectorTrigger(parentEntity, physics);
                }
                else
                {
                    if (connectorTrigger != null)
                        Destroy(connectorTrigger.gameObject);
                }
            }

            internal void DestroyConnector()
            {
                GameObject.Destroy(this);
            }

            internal override void OnBargeStopMoving()
            {
                SwitchConnector(false);
            }

            internal override bool IsModuleMoving()
            {
                if (springJoint == null || connectedRigidbody == null)
                    return false;

                if (physics.GetSpeed() < 0.5f)
                    return false;

                return true;
            }

            internal bool IsActive()
            {
                return isActive;
            }

            internal void BreakConnection()
            {
                if (springJoint != null)
                    Destroy(springJoint);
                connectedRigidbody = null;
            }

            internal override void UnloadModule()
            {
                base.UnloadModule();

                foreach (GameObject gameObject in gameObjects)
                    if (gameObject != null)
                        UnityEngine.GameObject.Destroy(gameObject);

                SwitchConnector(false);
            }

            class ConnectorTrigger : FacepunchBehaviour
            {
                BargePhysics physics;

                internal static ConnectorTrigger AttachConnectorTrigger(BaseEntity parentEntity, BargePhysics physics)
                {
                    GameObject gameObject = new GameObject("ConnectorTrigger");
                    gameObject.layer = (int)Rust.Layer.Reserved1;
                    gameObject.transform.localPosition = new Vector3(0, 0, 5.3f);
                    gameObject.transform.SetParent(parentEntity.transform, false);
                    ConnectorTrigger connectorTrigger = gameObject.AddComponent<ConnectorTrigger>();
                    connectorTrigger.Init(physics);
                    return connectorTrigger;
                }

                internal void Init(BargePhysics physics)
                {
                    this.physics = physics;
                    CreateSphereCollider();
                }

                void CreateSphereCollider()
                {
                    SphereCollider sphereCollider = this.gameObject.AddComponent<SphereCollider>();
                    sphereCollider.isTrigger = true;
                    sphereCollider.radius = 2.5f;
                }

                void OnTriggerEnter(Collider other)
                {
                    if (other == null)
                        return;

                    BaseEntity entity = other.ToBaseEntity();
                    if (entity == null || entity.net == null || entity.ShortPrefabName != "kayak")
                        return;

                    Barge barge = Barge.GetBargeByPhysicsEntityNetId(entity.net.ID.Value);

                    if (barge == null)
                        return;

                    BargePhysics bargePhysics = barge.GetBargePhysics();
                    physics.OnBargeWantConnect(bargePhysics);
                }
            }
        }

        class RampModule : BaseModule
        {
            Door door;

            internal static RampModule SpawnRampModule(BaseEntity parentEntity, Barge barge, CustomPrefabData customPrefabData)
            {
                RampModule rampModule = parentEntity.gameObject.AddComponent<RampModule>();
                rampModule.Init(parentEntity, barge, customPrefabData);
                return rampModule;
            }

            protected override void Init(BaseEntity parentEntity, Barge barge, CustomPrefabData customPrefabData)
            {
                base.Init(parentEntity, barge, customPrefabData);
                door = parentEntity.children.FirstOrDefault(x => x.IsExists() && x is Door) as Door;
                pressButton.pressDuration = 15f;
                parentEntity.Invoke(() => LoadModule(), 1f);
            }

            void LoadModule()
            {
                foreach (BaseEntity entity in parentEntity.children)
                {
                    SimpleLight simpleLight = entity as SimpleLight;
                    if (simpleLight != null)
                    {
                        simpleLight.SetFlag(BaseEntity.Flags.On, true);
                        simpleLight.SetFlag(BaseEntity.Flags.InUse, true);
                    }
                }
            }

            internal override void OnButtonPressed(BasePlayer player)
            {
                if (!door.IsExists())
                    return;

                door.SetOpen(!door.IsOpen());
            }
        }

        class CabineModule : BaseModule
        {
            BaseMountable driverSeat;
            BargePhysics physics;
            BaseEntity engineSoundEntity;
            ResourceExtractorFuelStorage fuelStorage;
            Dashboard dashboard;
            float lastFuelConsumtionTime;
            float targetConsumtionAmount;
            bool haveFuel;
            bool isEngineOne;

            internal static CabineModule SpawnCabineModule(BaseEntity parentEntity, Barge barge, CustomPrefabData customPrefabData)
            {
                CabineModule cabineModule = parentEntity.gameObject.AddComponent<CabineModule>();
                cabineModule.Init(parentEntity, barge, customPrefabData);
                parentEntity.Invoke(() => cabineModule.LoadModule(), 1f);
                return cabineModule;
            }

            void LoadModule()
            {
                SpawnDriverSeat();
                fuelStorage = parentEntity.children.FirstOrDefault(x => x is ResourceExtractorFuelStorage) as ResourceExtractorFuelStorage;
                fuelStorage.inventory.SetOnlyAllowedItem(ItemManager.FindItemDefinition(-946369541));
                fuelStorage.inventory.capacity = 1;
                fuelStorage.enableSaving = true;
                fuelStorage.SendNetworkUpdate();
                dashboard = Dashboard.SpawnDashboard(this, new Vector3(0.549f, 1.859f, 2.256f), new Vector3(341.874f, 180, 0), barge.GetBargePhysics());
                pressButton.pressDuration = 3f;
                physics = barge.GetBargePhysics();
                engineSoundEntity = BuildManager.SpawnChildEntity(parentEntity, "assets/content/vehicles/dpv/dpv.deployed.prefab", new Vector3(0, 0.5f, 0), Vector3.zero, isDecor: true);

                if (CalculateFuelAmount() == 0)
                {
                    BargeSaveData bargeSaveData = ins.bargeSaveDatas.FirstOrDefault(x => x.netId == barge.mainEntity.net.ID.Value);

                    if (bargeSaveData == null || bargeSaveData.fuelAmount == 0)
                        return;

                    Item fuelItem = ItemManager.CreateByName("lowgradefuel", bargeSaveData.fuelAmount);
                    fuelStorage.inventory.GiveItem(fuelItem);
                }
            }

            void SpawnDriverSeat()
            {
                driverSeat = MovableBaseMountable.CreateMovableBaseMountable("assets/prefabs/vehicle/seats/modularcardriverseat.prefab", parentEntity, new Vector3(0.17f, 1.15f, 2), Vector3.zero);
                driverSeat = parentEntity.children.FirstOrDefault(x => x is BaseMountable) as BaseMountable;
                GameObject gameObject = new GameObject("CustomDismountPosition");
                gameObject.transform.localPosition = new Vector3(-1.25f, 0, 0.2f);
                gameObject.transform.SetParent(driverSeat.transform, false);
                Transform[] dismountPoisions = new Transform[]
                {
                    gameObject.transform
                };
                driverSeat.dismountPositions = dismountPoisions;
            }

            internal override void OnButtonPressed(BasePlayer player)
            {
                if (player.isMounted)
                    return;

                driverSeat.AttemptMount(player, false);
                barge.StartMoving();
            }

            internal override bool IsModuleMoving()
            {
                return driverSeat.GetMounted() != null;
            }

            internal void FixedUpdate()
            {
                if (driverSeat == null)
                    return;

                BasePlayer driver = driverSeat.GetMounted();

                if (driver == null)
                    return;

                if (driver.serverInput.IsDown(BUTTON.JUMP) || driver.serverInput.WasJustPressed(BUTTON.JUMP) || driver.serverInput.WasJustReleased(BUTTON.JUMP))
                {
                    DismountPlayer(driver);
                    return;
                }

                if (fuelStorage == null)
                    return;

                if (!haveFuel)
                {
                    ConsumeFuel();
                    return;
                }

                float trottle;
                float steering;

                if (driver.serverInput.IsDown(BUTTON.FORWARD))
                    trottle = 1f;
                else if (driver.serverInput.IsDown(BUTTON.BACKWARD))
                    trottle = -1f;
                else
                    trottle = 0f;

                if (driver.serverInput.IsDown(BUTTON.LEFT))
                    steering = 1f;
                else if (driver.serverInput.IsDown(BUTTON.RIGHT))
                    steering = -1f;
                else
                    steering = 0f;

                Vector3 force = barge.transform.forward * trottle * barge.bargeConfig.engineConfig.powerScale * 0.5f;

                if (force.magnitude > 0)
                {
                    ConsumeFuel();

                    if (barge.IsBargeOnShoal(-1f))
                    {
                        if (TerrainMeta.HeightMap.GetHeight(barge.transform.position) < TerrainMeta.HeightMap.GetHeight(barge.transform.position + force.normalized))
                            return;
                    }

                    if (steering > 0)
                    {
                        physics.AddForceAtPosition(force * barge.bargeConfig.engineConfig.rotateScale, PositionDefiner.GetGlobalPosition(barge.transform, new Vector3(8, -3f, -13)));
                    }
                    else if (steering < 0)
                    {
                        physics.AddForceAtPosition(force * barge.bargeConfig.engineConfig.rotateScale, PositionDefiner.GetGlobalPosition(barge.transform, new Vector3(-8, -3f, -13)));
                    }
                    else
                    {
                        physics.AddForceAtPosition(force, PositionDefiner.GetGlobalPosition(barge.transform, new Vector3(-8, -3f, -13)));
                        physics.AddForceAtPosition(force, PositionDefiner.GetGlobalPosition(barge.transform, new Vector3(8, -3f, -13)));
                    }

                    SwitchEngine(true);
                }
            }

            void ConsumeFuel()
            {
                if (barge.bargeConfig.engineConfig.fuelScale == 0)
                {
                    haveFuel = true;
                    return;
                }

                float timeScienceConsumtion = UnityEngine.Time.realtimeSinceStartup - lastFuelConsumtionTime;

                if (timeScienceConsumtion >= 2)
                {
                    Item fuelItem = fuelStorage.inventory.itemList.FirstOrDefault(x => x != null && x.info.shortname == "lowgradefuel");

                    if (fuelItem == null)
                    {
                        if (haveFuel)
                        {
                            haveFuel = false;
                            SwitchEngine(false);
                        }
                        return;
                    }

                    haveFuel = true;
                    lastFuelConsumtionTime = UnityEngine.Time.realtimeSinceStartup;
                    targetConsumtionAmount += 1 * barge.bargeConfig.engineConfig.fuelScale;
                    int consumtionInInt = (int)targetConsumtionAmount;

                    if (consumtionInInt > 0)
                    {
                        targetConsumtionAmount -= consumtionInInt;

                        if (fuelItem.amount > consumtionInInt)
                        {
                            fuelItem.amount = fuelItem.amount - consumtionInInt;
                            fuelItem.MarkDirty();
                        }
                        else
                        {
                            fuelItem.Remove();
                        }
                    }
                }
            }

            internal int CalculateFuelAmount()
            {
                if (fuelStorage == null)
                    return 0;

                int result = 0;

                foreach (Item item in fuelStorage.inventory.itemList)
                    if (item != null && item.info.shortname == "lowgradefuel")
                        result += item.amount;

                return result;
            }

            void DismountPlayer(BasePlayer player)
            {
                driverSeat.DismountPlayer(player);
                player.Invoke(() => player.Teleport(driverSeat.dismountPositions[0].position), 0.01f);
                SwitchEngine(false);
            }

            void SwitchEngine(bool on)
            {
                if (isEngineOne == on)
                    return;

                isEngineOne = on;

                if (on)
                {
                    engineSoundEntity.SetFlag(BaseEntity.Flags.On, true);
                    dashboard.OnComponent();
                }
                else
                {
                    engineSoundEntity.SetFlag(BaseEntity.Flags.On, false);
                    dashboard.OffComponent();
                }
            }

            internal override void UnloadModule()
            {
                if (driverSeat.IsExists())
                    driverSeat.Kill();

                if (dashboard != null)
                    dashboard.KillDashboard();

                if (engineSoundEntity.IsExists())
                    engineSoundEntity.Kill();

                base.UnloadModule();
            }

            class Dashboard : FacepunchBehaviour
            {
                CabineModule cabineModule;
                BargePhysics bargePhysics;
                BaseEntity digitalClock;
                PowerCounter fuelDisplay;
                PowerCounter speedDisplay;
                Coroutine updateCoroutine;

                internal static Dashboard SpawnDashboard(CabineModule cabineModule, Vector3 localPosition, Vector3 localRotation, BargePhysics bargePhysics)
                {
                    BaseEntity digitalClock = BuildManager.SpawnChildEntity(cabineModule.parentEntity, "assets/prefabs/deployable/playerioents/digitalclock/electric.digitalclock.deployed.prefab", localPosition, localRotation, isDecor: true);
                    digitalClock.SetFlag(BaseEntity.Flags.Busy, true);
                    Dashboard dashboard = digitalClock.gameObject.AddComponent<Dashboard>();
                    dashboard.Init(cabineModule, digitalClock, bargePhysics);
                    return dashboard;
                }

                void Init(CabineModule cabineModule, BaseEntity digitalClock, BargePhysics bargePhysics)
                {
                    this.cabineModule = cabineModule;
                    this.digitalClock = digitalClock;
                    this.bargePhysics = bargePhysics;
                    fuelDisplay = SpawnDisplay(new Vector3(-0.124f, -0.069f, -0.032f));
                    speedDisplay = SpawnDisplay(new Vector3(0.124f, -0.069f, -0.032f));
                    updateCoroutine = ServerMgr.Instance.StartCoroutine(UpdateCorountine());
                }

                PowerCounter SpawnDisplay(Vector3 position)
                {
                    PowerCounter powerCounter = BuildManager.SpawnChildEntity(digitalClock, "assets/prefabs/deployable/playerioents/counter/counter.prefab", position, Vector3.zero, isDecor: false) as PowerCounter;
                    powerCounter.targetCounterNumber = int.MaxValue;
                    powerCounter.SetFlag(BaseEntity.Flags.Busy, true);
                    return powerCounter;
                }

                IEnumerator UpdateCorountine()
                {
                    while (cabineModule != null)
                    {
                        int speed = bargePhysics.GetSpeed();
                        int fuel = cabineModule.CalculateFuelAmount();

                        UpdateDisplay(fuelDisplay, fuel);
                        UpdateDisplay(speedDisplay, speed);

                        yield return CoroutineEx.waitForSeconds(0.7f);
                    }
                }

                void UpdateDisplay(PowerCounter display, int value)
                {
                    if (display.counterNumber == value)
                        return;

                    display.counterNumber = value;
                    display.SendNetworkUpdate();
                }

                internal void OnComponent()
                {
                    if (updateCoroutine != null)
                        ServerMgr.Instance.StopCoroutine(updateCoroutine);

                    updateCoroutine = ServerMgr.Instance.StartCoroutine(UpdateCorountine());
                    fuelDisplay.UpdateFromInput(1, 0);
                    speedDisplay.UpdateFromInput(1, 0);
                }

                internal void OffComponent()
                {
                    if (updateCoroutine != null)
                        ServerMgr.Instance.StopCoroutine(updateCoroutine);

                    updateCoroutine = null;
                    fuelDisplay.UpdateFromInput(0, 0);
                    speedDisplay.UpdateFromInput(0, 0);
                }

                internal void KillDashboard()
                {
                    digitalClock.Kill();
                }

                void OnDestroy()
                {
                    if (updateCoroutine != null)
                        ServerMgr.Instance.StopCoroutine(updateCoroutine);
                }
            }
        }

        class MovableBaseMountable : BaseMountable
        {
            internal static MovableBaseMountable CreateMovableBaseMountable(string chairPrefab, BaseEntity parentEntity, Vector3 localPosition, Vector3 localRotation)
            {
                BaseMountable baseMountable = GameManager.server.CreateEntity(chairPrefab, parentEntity.transform.position) as BaseMountable;
                baseMountable.enableSaving = false;
                MovableBaseMountable movableBaseMountable = baseMountable.gameObject.AddComponent<MovableBaseMountable>();
                BuildManager.CopySerializableFields(baseMountable, movableBaseMountable);
                baseMountable.StopAllCoroutines();
                UnityEngine.GameObject.DestroyImmediate(baseMountable, true);
                BuildManager.SetParent(parentEntity, movableBaseMountable, localPosition, localRotation);
                movableBaseMountable.Spawn();
                return movableBaseMountable;
            }

            public override void DismountAllPlayers()
            {
            }

            public override bool GetDismountPosition(BasePlayer player, out Vector3 res, bool silent = false)
            {
                res = player.transform.position + player.transform.right * 1.5f;
                return true;
            }

            public override void ScaleDamageForPlayer(BasePlayer player, HitInfo info)
            {

            }
        }

        abstract class BaseModule : FacepunchBehaviour
        {
            internal BaseEntity parentEntity;
            protected Barge barge;
            protected PressButton pressButton;

            protected virtual void Init(BaseEntity parentEntity, Barge barge, CustomPrefabData customPrefabData)
            {
                this.parentEntity = parentEntity;
                this.barge = barge;
                pressButton = parentEntity.children.FirstOrDefault(x => x.IsExists() && x is PressButton) as PressButton;
                pressButton.inputs = new IOEntity.IOSlot[0];
                pressButton.outputs = new IOEntity.IOSlot[0];
                pressButton.SetFlag(BaseEntity.Flags.InUse, true);
            }

            internal bool IsModuleEntity(BaseEntity entity)
            {
                BaseEntity entityParrent = entity.GetParentEntity();

                if (entityParrent == null || entityParrent.net == null)
                    return false;

                return entityParrent.net.ID.Value == parentEntity.net.ID.Value;
            }

            internal virtual void OnBargeStopMoving()
            {

            }

            internal virtual bool IsModuleMoving()
            {
                return false;
            }

            internal abstract void OnButtonPressed(BasePlayer player);

            internal void UpdateModuleVisibiliy()
            {
                if (!parentEntity.IsExists())
                    return;

                foreach (BaseEntity entity in parentEntity.children)
                {
                    BuildingBlock buildingBlock = entity as BuildingBlock;
                    if (!buildingBlock.IsExists())
                        continue;

                    buildingBlock.limitNetworking = true;
                    buildingBlock.limitNetworking = false;
                }
            }

            internal virtual void UnloadModule()
            {
                Destroy(this);
            }
        }

        class BoatConnectionTrigger : FacepunchBehaviour
        {
            static HashSet<BoatConnectionTrigger> boatConnectionTriggers = new HashSet<BoatConnectionTrigger>();
            BaseBoat baseBoat;

            internal static void UpdateAllBoats()
            {
                foreach (BaseMountable baseMountable in BaseMountable.AllMountables)
                {
                    BaseBoat baseBoat = baseMountable as BaseBoat;
                    if (baseBoat == null)
                        continue;

                    TryAddBoatConnectionTrigger(baseBoat);
                }
            }

            internal static void TryAddBoatConnectionTrigger(BaseBoat baseBoat)
            {
                BoatConnectionTrigger boatConnectionTrigger = baseBoat.gameObject.AddComponent<BoatConnectionTrigger>();
                boatConnectionTriggers.Add(boatConnectionTrigger);
                boatConnectionTrigger.Init(baseBoat);
            }

            void Init(BaseBoat baseBoat)
            {
                this.baseBoat = baseBoat;
            }

            void OnCollisionEnter(Collision collision)
            {
                if (collision == null || collision.collider == null)
                    return;

                BaseEntity baseEntity = collision.GetEntity();
                if (baseEntity == null)
                    return;

                if (baseEntity.ShortPrefabName != "newyeargong.deployed")
                    return;

                Barge barge = Barge.GetBargeByEntity(baseEntity);
                if (barge == null)
                    return;

                BargePhysics bargePhysics = barge.GetBargePhysics();
                bargePhysics.OnBoatWantConnect(baseBoat);
            }

            internal static void Unload()
            {
                foreach (BoatConnectionTrigger boatConnectionTrigger in boatConnectionTriggers)
                    if (boatConnectionTrigger != null)
                        GameObject.Destroy(boatConnectionTrigger);
            }
        }


        class CollisionDisabler : FacepunchBehaviour
        {
            Barge barge;
            HashSet<Collider> colliders = new HashSet<Collider>();

            internal static void AttachCollisonDisabler(BaseEntity baseEntity, Barge barge)
            {
                CollisionDisabler collisionDisabler = baseEntity.gameObject.AddComponent<CollisionDisabler>();
                collisionDisabler.barge = barge;

                foreach (Collider collider in baseEntity.GetComponentsInChildren<Collider>())
                    if (collider != null)
                        collisionDisabler.colliders.Add(collider);
            }

            void OnCollisionEnter(Collision collision)
            {
                if (collision == null || barge == null || collision.collider == null)
                    return;

                BaseEntity baseEntity = collision.GetEntity();
                if (baseEntity == null)
                    return;

                if (barge.IsBargeEntity(baseEntity))
                    IgnoreCollider(collision.collider);
            }

            void IgnoreCollider(Collider otherCollider)
            {
                foreach (Collider collider in colliders)
                    if (collider != null)
                        Physics.IgnoreCollision(collider, otherCollider);
            }
        }

        static class BuildManager
        {
            internal static BuildingBlock SpawnChildBuildingBlock(BuildingBlockData buildingBlockData, BaseEntity parentEntity)
            {
                BuildingBlock buildingBlock = CreateEntity(buildingBlockData.prefabName, parentEntity.transform.position, Quaternion.identity, 0, true) as BuildingBlock;
                SetParent(parentEntity, buildingBlock, buildingBlockData.position.ToVector3(), buildingBlockData.rotation.ToVector3());
                buildingBlock.AttachToBuilding(BuildingManager.server.NewBuildingID());
                buildingBlock.grounded = true;
                buildingBlock.cachedStability = 1;
                buildingBlock.Spawn();
                BuildingManager.server.decayEntities.Remove(buildingBlock);

                BuildingGrade.Enum buildingGrade = (BuildingGrade.Enum)buildingBlockData.grade;
                buildingBlock.ChangeGradeAndSkin(buildingGrade, buildingBlockData.skin);

                if (buildingBlockData.color != 0)
                    buildingBlock.SetCustomColour(buildingBlockData.color);

                return buildingBlock;
            }

            internal static void HideParentEntityForMoment(BuildingBlock buildingBlock)
            {
                BaseEntity parentEntity = buildingBlock.GetParentEntity();

                if (parentEntity == null)
                    return;

                BaseEntity baseEntity = buildingBlock.GetParentEntity();
                buildingBlock.parentEntity.Set(null);
                buildingBlock.Invoke(() =>
                {
                    buildingBlock.SetParent(parentEntity, false, true);
                    BuildingVisibilityManager.UpdateBuildingBlockVisibility(buildingBlock);
                }, 0.2f);
            }

            internal static void HideWirePoints(IOEntity entity)
            {
                entity.inputs = Array.Empty<IOEntity.IOSlot>();

                foreach (IOEntity.IOSlot slot in entity.outputs)
                    slot.type = IOEntity.IOType.Generic;
            }


            internal static void UpdateMeshColliders(BaseEntity entity)
            {
                MeshCollider[] meshColliders = entity.GetComponentsInChildren<MeshCollider>();

                for (int i = 0; i < meshColliders.Length; i++)
                {
                    MeshCollider meshCollider = meshColliders[i];
                }
            }

            internal static BaseEntity SpawnRegularEntity(string prefabName, Vector3 position, Quaternion rotation, ulong skinId = 0, bool enableSaving = false)
            {
                BaseEntity entity = CreateEntity(prefabName, position, rotation, skinId, enableSaving);
                entity.Spawn();
                return entity;
            }

            internal static BaseEntity SpawnStaticEntity(string prefabName, Vector3 position, Quaternion rotation, ulong skinId = 0)
            {
                BaseEntity entity = CreateEntity(prefabName, position, rotation, skinId, false);
                DestroyUnnessesaryComponents(entity);

                StabilityEntity stabilityEntity = entity as StabilityEntity;
                if (stabilityEntity != null)
                    stabilityEntity.grounded = true;

                BaseCombatEntity baseCombatEntity = entity as BaseCombatEntity;
                if (baseCombatEntity != null)
                    baseCombatEntity.pickup.enabled = false;

                entity.Spawn();
                return entity;
            }

            internal static BaseEntity SpawnChildEntity(BaseEntity parrentEntity, string prefabName, LocationConfig locationConfig, ulong skinId, bool isDecor)
            {
                Vector3 localPosition = locationConfig.position.ToVector3();
                Vector3 localRotation = locationConfig.rotation.ToVector3();
                return SpawnChildEntity(parrentEntity, prefabName, localPosition, localRotation, skinId, isDecor);
            }

            internal static BaseEntity SpawnChildEntity(BaseEntity parrentEntity, string prefabName, Vector3 localPosition, Vector3 localRotation, ulong skinId = 0, bool isDecor = true, bool enableSaving = false)
            {
                BaseEntity entity = isDecor ? CreateDecorEntity(prefabName, parrentEntity.transform.position, Quaternion.identity, skinId) : CreateEntity(prefabName, parrentEntity.transform.position, Quaternion.identity, skinId, enableSaving);
                SetParent(parrentEntity, entity, localPosition, localRotation);
                DestroyUnnessesaryComponents(entity);
                if (isDecor)
                    DestroyDecorComponents(entity);

                entity.Spawn();
                UpdateMeshColliders(entity);
                return entity;
            }

            internal static void UpdateEntityMaxHealth(BaseCombatEntity baseCombatEntity, float maxHealth)
            {
                baseCombatEntity.startHealth = maxHealth;
                baseCombatEntity.InitializeHealth(maxHealth, maxHealth);
            }

            internal static BaseEntity CreateEntity(string prefabName, Vector3 position, Quaternion rotation, ulong skinId, bool enableSaving)
            {
                BaseEntity entity = GameManager.server.CreateEntity(prefabName, position, rotation);
                entity.enableSaving = enableSaving;
                entity.skinID = skinId;
                return entity;
            }

            static BaseEntity CreateDecorEntity(string prefabName, Vector3 position, Quaternion rotation, ulong skinId = 0, bool enableSaving = false)
            {
                BaseEntity entity = CreateEntity(prefabName, position, rotation, skinId, enableSaving);

                BaseEntity trueBaseEntity = entity.gameObject.AddComponent<BaseEntity>();
                CopySerializableFields(entity, trueBaseEntity);
                UnityEngine.Object.DestroyImmediate(entity, true);
                entity.SetFlag(BaseEntity.Flags.Busy, true);
                entity.SetFlag(BaseEntity.Flags.Locked, true);

                return trueBaseEntity;
            }

            internal static void SetParent(BaseEntity parrentEntity, BaseEntity childEntity, Vector3 localPosition, Vector3 localRotation)
            {
                childEntity.transform.localPosition = localPosition;
                childEntity.transform.localEulerAngles = localRotation;
                childEntity.SetParent(parrentEntity);
            }

            static void DestroyDecorComponents(BaseEntity entity)
            {
                Component[] components = entity.GetComponentsInChildren<Component>();

                for (int i = 0; i < components.Length; i++)
                {
                    Component component = components[i];

                    EntityCollisionMessage entityCollisionMessage = component as EntityCollisionMessage;

                    if (entityCollisionMessage != null || (component != null && component.name != entity.PrefabName))
                    {
                        Transform transform = component as Transform;
                        if (transform != null)
                            continue;

                        Collider collider = component as Collider;
                        if (collider != null && collider is MeshCollider == false)
                            continue;

                        if (component is Model)
                            continue;

                        UnityEngine.GameObject.DestroyImmediate(component as UnityEngine.Object);
                    }
                }
            }

            static void DestroyUnnessesaryComponents(BaseEntity entity)
            {
                DestroyEntityConponent<GroundWatch>(entity);
                DestroyEntityConponent<DestroyOnGroundMissing>(entity);
                DestroyEntityConponent<TriggerHurtEx>(entity);

                if (entity is BradleyAPC == false)
                    DestroyEntityConponent<Rigidbody>(entity);
            }

            internal static void DestroyEntityConponent<TypeForDestroy>(BaseEntity entity)
            {
                if (entity == null)
                    return;

                TypeForDestroy component = entity.GetComponent<TypeForDestroy>();
                if (component != null)
                    UnityEngine.GameObject.DestroyImmediate(component as UnityEngine.Object);
            }

            internal static void DestroyEntityConponents<TypeForDestroy>(BaseEntity entity)
            {
                if (entity == null)
                    return;

                TypeForDestroy[] components = entity.GetComponentsInChildren<TypeForDestroy>();

                for (int i = 0; i < components.Length; i++)
                {
                    TypeForDestroy component = components[i];

                    if (component != null)
                        UnityEngine.GameObject.DestroyImmediate(component as UnityEngine.Object);
                }
            }

            internal static void CopySerializableFields<T>(T src, T dst)
            {
                FieldInfo[] srcFields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (FieldInfo field in srcFields)
                {
                    object value = field.GetValue(src);
                    field.SetValue(dst, value);
                }
            }
        }

        static class PositionDefiner
        {
            internal static Vector3 GetGroundPositionInPoint(Vector3 position)
            {
                position.y = 100;
                RaycastHit raycastHit;

                if (Physics.Raycast(position, Vector3.down, out raycastHit, 500, 1 << 16 | 1 << 23))
                    position.y = raycastHit.point.y;

                return position;
            }

            internal static Vector3 GetGlobalPosition(Transform parentTransform, Vector3 position)
            {
                return parentTransform.transform.TransformPoint(position);
            }

            internal static Vector3 GetLocalPosition(Transform parentTransform, Vector3 globalPosition)
            {
                return parentTransform.transform.InverseTransformPoint(globalPosition);
            }

            internal static Quaternion GetGlobalRotation(Transform parentTransform, Vector3 rotation)
            {
                return parentTransform.rotation * Quaternion.Euler(rotation);
            }

            internal static bool GetNavmeshInPoint(Vector3 position, float radius, out NavMeshHit navMeshHit)
            {
                return NavMesh.SamplePosition(position, out navMeshHit, radius, 1);
            }

            internal static bool IsPositionOnCargoPath(Vector3 position)
            {
                foreach (CargoShip.HarborInfo harbotInfo in CargoShip.harbors)
                {
                    IAIPathNode pathNode = harbotInfo.harborPath.GetClosestToPoint(position);

                    if (Vector3.Distance(pathNode.Position, position) < 90)
                        return true;
                }

                float distanceToCargoPath = GetDistanceToCargoPath(position);
                return distanceToCargoPath < 60;
            }

            static float GetDistanceToCargoPath(Vector3 position)
            {
                int index = GetNearIndexPathCargo(position);
                int indexNext = TerrainMeta.Path.OceanPatrolFar.Count - 1 == index ? 0 : index + 1;
                int indexPrevious = index == 0 ? TerrainMeta.Path.OceanPatrolFar.Count - 1 : index - 1;
                float distanceNext = GetDistanceToCargoPath(position, index, indexNext);
                float distancePrevious = GetDistanceToCargoPath(position, indexPrevious, index);
                return distanceNext < distancePrevious ? distanceNext : distancePrevious;
            }

            static int GetNearIndexPathCargo(Vector3 position)
            {
                int index = 0;
                float distance = float.MaxValue;

                for (int i = 0; i < TerrainMeta.Path.OceanPatrolFar.Count; i++)
                {
                    Vector3 vector3 = TerrainMeta.Path.OceanPatrolFar[i];
                    float single = Vector3.Distance(position, vector3);

                    if (single < distance)
                    {
                        index = i;
                        distance = single;
                    }
                }

                return index;
            }

            static float GetDistanceToCargoPath(Vector3 position, int index1, int index2)
            {
                Vector3 pos1 = TerrainMeta.Path.OceanPatrolFar[index1];
                Vector3 pos2 = TerrainMeta.Path.OceanPatrolFar[index2];

                float distance1 = Vector3.Distance(position, pos1);
                float distance2 = Vector3.Distance(position, pos2);
                float distance12 = Vector3.Distance(pos1, pos2);

                float p = (distance1 + distance2 + distance12) / 2;

                return (2 / distance12) * (float)Math.Sqrt(p * (p - distance1) * (p - distance2) * (p - distance12));
            }
        }

        static class NotifyManager
        {
            internal static void PrintInfoMessage(BasePlayer player, string langKey, params object[] args)
            {
                if (player == null)
                    ins.PrintWarning(ClearColorAndSize(GetMessage(langKey, null, args)));
                else
                    ins.PrintToChat(player, GetMessage(langKey, player.UserIDString, args));
            }

            internal static void PrintError(BasePlayer player, string langKey, params object[] args)
            {
                if (player == null)
                    ins.PrintError(ClearColorAndSize(GetMessage(langKey, null, args)));
                else
                    ins.PrintToChat(player, GetMessage(langKey, player.UserIDString, args));
            }



            internal static void PrintLogMessage(string langKey, params object[] args)
            {
                for (int i = 0; i < args.Length; i++)
                    if (args[i] is int)
                        args[i] = GetTimeMessage(null, (int)args[i]);

                ins.Puts(ClearColorAndSize(GetMessage(langKey, null, args)));
            }

            internal static void PrintWarningMessage(string langKey, params object[] args)
            {
                ins.PrintWarning(ClearColorAndSize(GetMessage(langKey, null, args)));
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
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    if (player != null)
                        SendMessageToPlayer(player, langKey, args);

                TrySendDiscordMessage(langKey, args);
            }

            internal static void SendMessageToPlayer(BasePlayer player, string langKey, params object[] args)
            {
                for (int i = 0; i < args.Length; i++)
                    if (args[i] is int)
                        args[i] = GetTimeMessage(player.UserIDString, (int)args[i]);

                RedefinedMessageConfig redefinedMessageConfig = GetRedefinedMessageConfig(langKey);

                if (redefinedMessageConfig != null && !redefinedMessageConfig.isEnable)
                    return;

                string playerMessage = GetMessage(langKey, player.UserIDString, args);

                if (redefinedMessageConfig != null)
                    SendMessage(redefinedMessageConfig, player, playerMessage);
                else
                    SendMessage(ins._config.notifyConfig, player, playerMessage);
            }

            static void SendMessage(BaseMessageConfig baseMessageConfig, BasePlayer player, string playerMessage)
            {
                if (baseMessageConfig.chatConfig.isEnabled)
                    ins.PrintToChat(player, playerMessage);

                if (baseMessageConfig.gameTipConfig.isEnabled)
                    player.SendConsoleCommand("gametip.showtoast", baseMessageConfig.gameTipConfig.style, ClearColorAndSize(playerMessage), string.Empty);

                if (baseMessageConfig.guiAnnouncementsConfig.isEnabled && ins.plugins.Exists("guiAnnouncementsConfig"))
                    ins.GUIAnnouncements?.Call("CreateAnnouncement", ClearColorAndSize(playerMessage), baseMessageConfig.guiAnnouncementsConfig.bannerColor, baseMessageConfig.guiAnnouncementsConfig.textColor, player, baseMessageConfig.guiAnnouncementsConfig.apiAdjustVPosition);

                if (baseMessageConfig.notifyPluginConfig.isEnabled && ins.plugins.Exists("Notify"))
                    ins.Notify?.Call("SendNotify", player, baseMessageConfig.notifyPluginConfig.type, ClearColorAndSize(playerMessage));
            }

            static RedefinedMessageConfig GetRedefinedMessageConfig(string langKey)
            {
                return ins._config.notifyConfig.redefinedMessages.FirstOrDefault(x => x.langKey == langKey);
            }

            internal static string GetTimeMessage(string userIDString, int seconds)
            {
                string message = "";

                TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
                if (timeSpan.Hours > 0) message += $" {timeSpan.Hours} {GetMessage("Hours", userIDString)}";
                if (timeSpan.Minutes > 0) message += $" {timeSpan.Minutes} {GetMessage("Minutes", userIDString)}";
                if (message == "") message += $" {timeSpan.Seconds} {GetMessage("Seconds", userIDString)}";

                return message;
            }

            static void TrySendDiscordMessage(string langKey, params object[] args)
            {
                if (CanSendDiscordMessage(langKey))
                {
                    for (int i = 0; i < args.Length; i++)
                        if (args[i] is int)
                            args[i] = GetTimeMessage(null, (int)args[i]);

                    object fields = new[] { new { name = ins.Title, value = ClearColorAndSize(GetMessage(langKey, null, args)), inline = false } };
                    ins.DiscordMessages?.Call("API_SendFancyMessage", ins._config.notifyConfig.discordMessagesConfig.webhookUrl, "", ins._config.notifyConfig.discordMessagesConfig.embedColor, JsonConvert.SerializeObject(fields), null, ins);
                }
            }

            static bool CanSendDiscordMessage(string langKey)
            {
                return ins._config.notifyConfig.discordMessagesConfig.keys.Contains(langKey) && ins._config.notifyConfig.discordMessagesConfig.isEnabled && !string.IsNullOrEmpty(ins._config.notifyConfig.discordMessagesConfig.webhookUrl) && ins._config.notifyConfig.discordMessagesConfig.webhookUrl != "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
            }
        }

        static class LootManager
        {
            internal static void GiveItemToPLayer(BasePlayer player, ItemConfig itemConfig, int amount)
            {
                Item item = CreateItem(itemConfig, amount);
                if (item == null)
                    return;

                GiveItemToPLayer(player, item);
            }

            static void GiveItemToPLayer(BasePlayer player, Item item)
            {
                int slots = player.inventory.containerMain.capacity + player.inventory.containerBelt.capacity;
                int taken = player.inventory.containerMain.itemList.Count + player.inventory.containerBelt.itemList.Count;

                if (slots - taken > 0)
                    player.inventory.GiveItem(item);
                else
                    item.Drop(player.transform.position, Vector3.up);
            }

            internal static Item CreateItem(ItemConfig itemConfig, int amount)
            {
                Item item = ItemManager.CreateByName(itemConfig.shortname, amount, itemConfig.skin);

                if (itemConfig.name != "")
                    item.name = itemConfig.name;

                return item;
            }
        }

        internal static class MapSaver
        {
            static Dictionary<string, string> colliderPrefabNames = new Dictionary<string, string>
            {
                ["fence_a"] = "assets/prefabs/misc/xmas/icewalls/icewall.prefab",
                ["christmas_present_LOD0"] = "assets/prefabs/misc/xmas/sleigh/presentdrop.prefab",
                ["snowman_LOD1"] = "assets/prefabs/misc/xmas/snowman/snowman.deployed.prefab",
                ["giftbox_LOD0"] = "assets/prefabs/misc/xmas/giftbox/giftbox_loot.prefab"
            };

            internal static void CreateOrAddNewData(string customizationPresetName)
            {
                BargeCustomizeData newCustomizeProfile = new BargeCustomizeData
                {
                    buildingBlocks = new HashSet<BuildingBlockData>(),
                    decorEntities = new HashSet<EntityData>(),
                    regularEntities = new HashSet<EntityData>(),
                    boxCollisersData = new HashSet<BoxColliderData>(),
                    buoyancyPoints = new List<string>(),
                };

                CheckAndSaveColliders(ref newCustomizeProfile);
                ins.SaveDataFile(newCustomizeProfile, customizationPresetName);
            }

            static void CheckAndSaveColliders(ref BargeCustomizeData bargeData)
            {
                List<Collider> colliders = Physics.OverlapSphere(Vector3.zero, 50).OrderBy(x => x.transform.position.z);

                foreach (Collider collider in colliders)
                    TrySaveCollder(collider, ref bargeData);
            }

            static void TrySaveCollder(Collider collider, ref BargeCustomizeData bargeData)
            {
                BaseEntity entity = collider.ToBaseEntity();

                if (entity != null)
                    SaveRegularEntity(entity, ref bargeData);
                else if (collider.name.Contains("building core"))
                    SaveBuildingBlock(collider, ref bargeData);
                else
                    SaveCollider(collider, ref bargeData);
            }

            static void SaveBuildingBlock(Collider collider, ref BargeCustomizeData bargeData)
            {
                BuildingBlockData buildingBlockData = GetBuildingBlockConfig(collider);

                if (buildingBlockData != null && !bargeData.buildingBlocks.Any(x => x.prefabName == buildingBlockData.prefabName && x.position == buildingBlockData.position && x.rotation == buildingBlockData.rotation))
                    bargeData.buildingBlocks.Add(buildingBlockData);
            }

            static BuildingBlockData GetBuildingBlockConfig(Collider collider)
            {
                BuildingBlockData buildingBlockData = new BuildingBlockData()
                {
                    prefabName = collider.name,
                    position = $"({collider.transform.position.x}, {collider.transform.position.y}, {collider.transform.position.z})",
                    rotation = collider.transform.eulerAngles.ToString()
                };

                if (buildingBlockData.prefabName.Contains(".metal"))
                {
                    buildingBlockData.prefabName = buildingBlockData.prefabName.Replace(".metal", "");
                    buildingBlockData.grade = 3;
                    buildingBlockData.skin = 10221;
                    buildingBlockData.color = 11;
                }

                return buildingBlockData;
            }

            static void SaveRegularEntity(BaseEntity entity, ref BargeCustomizeData bargeData)
            {
                if (entity.PrefabName != "assets/scenes/prefabs/trainyard/subents/coaling_tower_fuel_storage.entity.prefab")
                    return;

                EntityData decorLocationConfig = GetDecorEntityConfig(entity);

                if (decorLocationConfig != null && !bargeData.decorEntities.Any(x => x.prefabName == decorLocationConfig.prefabName && x.position == decorLocationConfig.position && x.rotation == decorLocationConfig.rotation))
                    bargeData.decorEntities.Add(decorLocationConfig);
            }

            static EntityData GetDecorEntityConfig(BaseEntity entity)
            {
                ulong skin = entity.skinID;

                return new EntityData
                {
                    prefabName = entity.PrefabName,
                    skin = skin,
                    position = $"({entity.transform.position.x}, {entity.transform.position.y}, {entity.transform.position.z})",
                    rotation = entity.transform.eulerAngles.ToString()
                };
            }

            static void SaveCollider(Collider collider, ref BargeCustomizeData bargeData)
            {
                EntityData colliderEntityConfig = GetColliderConfigAsBaseEntity(collider);

                if (colliderEntityConfig != null && !bargeData.decorEntities.Any(x => x.prefabName == colliderEntityConfig.prefabName && x.position == colliderEntityConfig.position && x.rotation == colliderEntityConfig.rotation))
                    bargeData.decorEntities.Add(colliderEntityConfig);
            }

            static EntityData GetColliderConfigAsBaseEntity(Collider collider)
            {
                string prefabName = "";

                if (!colliderPrefabNames.TryGetValue(collider.name, out prefabName))
                    return null;

                return new EntityData
                {
                    prefabName = prefabName,
                    skin = 0,
                    position = $"({collider.transform.position.x}, {collider.transform.position.y}, {collider.transform.position.z})",
                    rotation = collider.transform.eulerAngles.ToString()
                };
            }
        }
        #endregion Classes 

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GotBarge"] = "Вы получили Баржу!",

                ["BlockGrade"] = "Постройка из камня/МВК слишком тяжелая и не может плавать!",
                ["TooHigh"] = "Постройка слишком высокая. Максимальное количество этажей - {0}",
                ["OutsideBarge"] = "Запрещено строиться за пределами баржи",
                ["NotAuthorized"] = "Вы не авторизованы!",
                ["AnchorBarge"] = "Поставьте баржу на якорь!",

                ["WrongPosition"] = "Баржа в неверном положении. Поставьте ее на якорь еще раз.",
                ["WrongPositionOnShole"] = "Баржа на мелководье. Строительство запрещено!",

                ["BlockedOnBarge"] = "Запрещено использовать на барже!",
                ["BlockedWhileMoving"] = "Запрещено во время движения!",
                ["NotEnoughSpace"] = "Недостаточно места!",

            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ConfigNotFound_Exeption"] = "Barge configuration not found! ({0})",
                ["DataFileNotFound_Exeption"] = "Data file not found! ({0})",
                ["PlayerNotFound_Exeption"] = "Player not found! ({0})",
                ["DataNotFound_Exeption"] = "Data files were not found, or are corrupted. Move the contents of the data folder from the archive to the oxide/data folder on your server!",

                ["GotBarge"] = "You've got a Barge!",

                ["BlockGrade"] = "The building made of stone/HQM is too heavy and cannot float!",
                ["TooHigh"] = "The building is too high. Maximum number of floors - {0}",
                ["OutsideBarge"] = "It is forbidden to build outside the barge",
                ["NotAuthorized"] = "You are not authorized!",
                ["AnchorBarge"] = "Anchor the barge!",

                ["WrongPosition"] = "The barge is in the wrong position. Try anchoring it again!",
                ["WrongPositionOnShole"] = "A barge in shallow water! Building blocked!",

                ["BlockedOnBarge"] = "It is not allowed to use it on a barge!",
                ["BlockedWhileMoving"] = "It is prohibited while moving!",
                ["NotEnoughSpace"] = "Not enough space!",
            }, this);
        }

        internal static string GetMessage(string langKey, string userID) => ins.lang.GetMessage(langKey, ins, userID);

        internal static string GetMessage(string langKey, string userID, params object[] args) => (args.Length == 0) ? GetMessage(langKey, userID) : string.Format(GetMessage(langKey, userID), args);
        #endregion Lang

        #region Data
        HashSet<BargeSaveData> bargeSaveDatas = new HashSet<BargeSaveData>();

        public class BargeSaveData
        {
            public ulong netId;
            public string bargePreset;
            public bool isServerSpawn;
            public int fuelAmount;
        }

        Dictionary<string, BargeCustomizeData> bargeCustomizationDatas = new Dictionary<string, BargeCustomizeData>();
        Dictionary<string, CustomPrefabData> moduleCustomizationDatas = new Dictionary<string, CustomPrefabData>();

        bool TryLoadData()
        {
            foreach (BargeConfig bargeConfig in _config.bargeConfigs)
            {
                if (!TryLoadBargeDataFile(bargeConfig.dataFileName))
                    return false;

                foreach (PresetLocationConfig presetLocationConfig in bargeConfig.modules)
                    if (!TryLoadModuleDataFile(presetLocationConfig.presetName))
                        return false;
            }

            bargeSaveDatas = LoadDataFile<HashSet<BargeSaveData>>("save");

            if (bargeSaveDatas == null)
                bargeSaveDatas = new HashSet<BargeSaveData>();

            return true;
        }

        bool TryLoadBargeDataFile(string path)
        {
            BargeCustomizeData bargeProfile = LoadDataFile<BargeCustomizeData>($"Platforms/{path}");

            if (bargeProfile == null || bargeProfile.buoyancyPoints == null)
                return false;

            if (!bargeCustomizationDatas.ContainsKey(path))
                bargeCustomizationDatas.Add(path, bargeProfile);

            return true;
        }

        bool TryLoadModuleDataFile(string path)
        {
            if (moduleCustomizationDatas.ContainsKey(path))
                return true;

            CustomPrefabData moduleProfile = LoadDataFile<CustomPrefabData>($"Modules/{path}");

            if (moduleProfile == null || moduleProfile.decorEntities == null)
                return false;

            moduleCustomizationDatas.Add(path, moduleProfile);
            return true;
        }

        Type LoadDataFile<Type>(string path)
        {
            string fullPath = $"{ins.Name}/{path}";
            return Interface.Oxide.DataFileSystem.ReadObject<Type>(fullPath);
        }

        void SaveDataFile<Type>(Type objectForSaving, string path)
        {
            string fullPath = $"{ins.Name}/{path}";
            Interface.Oxide.DataFileSystem.WriteObject(fullPath, objectForSaving);
        }

        public class BargeCustomizeData : CustomPrefabData
        {
            [JsonProperty("Buoyancy Points")] public List<string> buoyancyPoints { get; set; }
            [JsonProperty("Parent Collider Size")] public string size { get; set; }
        }

        public class CustomPrefabData
        {
            [JsonProperty("The location of the basic building blocks (Cannot be destroyed)")] public HashSet<BuildingBlockData> buildingBlocks { get; set; }
            [JsonProperty("Regular Entities")] public HashSet<EntityData> regularEntities { get; set; }
            [JsonProperty("Decor Entities")] public HashSet<EntityData> decorEntities { get; set; }
            [JsonProperty("Box Colliders")] public HashSet<BoxColliderData> boxCollisersData { get; set; }
        }

        public class BoxColliderData
        {
            [JsonProperty("Local Position")] public string localPosition { get; set; }
            [JsonProperty("Local Rotation")] public string localRotation { get; set; }
            [JsonProperty("Size")] public string size { get; set; }
        }

        public class BoxColliderInfo
        {
            public Vector3 localPosition;
            public Vector3 localRotation;
            public Vector3 size;

            public BoxColliderInfo(BoxColliderData boxColliderData)
            {
                localPosition = boxColliderData.localPosition.ToVector3();
                localRotation = boxColliderData.localRotation.ToVector3();
                size = boxColliderData.size.ToVector3();
            }

            public BoxColliderInfo(Vector3 localPosition, Vector3 localRotation, Vector3 size)
            {
                this.localPosition = localPosition;
                this.localRotation = localRotation;
                this.size = size;
            }
        }

        public class BuildingBlockData : EntityData
        {
            [JsonProperty("Grade [0 - 4]", Order = 102)] public int grade { get; set; }
            [JsonProperty("Color", Order = 103)] public uint color { get; set; }
        }

        public class EntityData
        {
            [JsonProperty("Prefab")] public string prefabName { get; set; }
            [JsonProperty("Skin")] public ulong skin { get; set; }
            [JsonProperty("Position", Order = 100)] public string position { get; set; }
            [JsonProperty("Rotation", Order = 101)] public string rotation { get; set; }
        }

        public class BuildingBlockSaveData
        {
            public ulong buildingBlockNetID;
            public float healthFraction;
            public int grade;
            public ulong skinid;
            public uint customColour;

            internal BuildingBlockSaveData(ulong buildingBlockNetID, float healthFraction, int grade, ulong skinid, uint customColour)
            {
                this.buildingBlockNetID = buildingBlockNetID;
                this.healthFraction = healthFraction;
                this.grade = grade;
                this.skinid = skinid;
                this.customColour = customColour;
            }
        }

        public class IoEntitySaveData
        {
            public ulong ioEntityNetId;
            public HashSet<IOSlotData> inputSlotDatas;
            public HashSet<IOSlotData> outputSlotDatas;

            public IoEntitySaveData(ulong ioEntityNetId, HashSet<IOSlotData> inputSlotDatas, HashSet<IOSlotData> outputSlotDatas)
            {
                this.ioEntityNetId = ioEntityNetId;
                this.inputSlotDatas = inputSlotDatas;
                this.outputSlotDatas = outputSlotDatas;
            }
        }

        public class IOSlotData
        {
            public HashSet<Vector3> linePoints = new HashSet<Vector3>();

            public IOSlotData(HashSet<Vector3> linePoints)
            {
                this.linePoints = linePoints;
            }
        }
        #endregion Data

        #region Config
        private PluginConfig _config;

        protected override void LoadDefaultConfig()
        {
            _config = PluginConfig.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(_config, true);
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        public class MainConfig
        {
            [JsonProperty(en ? "The distance outside the grid on the map that a barge can travel (-1 is not limited)" : "Расстояние за пределами сетки на карте, на которое может уплыть баржа (-1 не ограничивать)")] public float maxShoreDistance { get; set; }
            [JsonProperty(en ? "Prohibit barges from approaching fishing villages [true/false]" : "Запретить баржам приближаться к рыбацким деревням [true/false]")] public bool blockFishingVillage { get; set; }
            [JsonProperty(en ? "Items whose installation is prohibited on the barge" : "Предметы, установка которых запрещена на барже")] public HashSet<string> bloskedItemShortnames { get; set; }
            [JsonProperty(en ? "Commands that are prohibited on the barge" : "Список команд, которые запрещены на барже")] public HashSet<string> blockedCommands { get; set; }
        }

        public class SpawnConfig
        {
            [JsonProperty(en ? "Turn on the spawn of barges on the map? [true/false]" : "Включить спавн барж на карте? [true/false]")] public bool isSpawnEnabled { get; set; }
            [JsonProperty(en ? "Maximum number of automatically spawned barges" : "Максимальное количество автоматически заспавненных барж на сервере")] public int maxBargeCount { get; set; }
            [JsonProperty(en ? "Minimum time between the spawning of barges" : "Минимальное время между спавном барж [sec]")] public int minSpawnTime { get; set; }
            [JsonProperty(en ? "Maximum time between the spawning of barges" : "Maксимальное время между спавном барж [sec]")] public int maxSpawnTime { get; set; }
            [JsonProperty(en ? "Barge Preset - probability" : "Пресет баржи - вероятность")] public Dictionary<string, float> probabilities { get; set; }
            [JsonProperty(en ? "List of monuments for spawn" : "Список монументов для спавна")] public HashSet<SpawnMonumentConfig> monuments { get; set; }
        }

        public class SpawnMonumentConfig
        {
            [JsonProperty(en ? "The name of the monument" : "Название монумента")] public string monumentName { get; set; }
            [JsonProperty(en ? "Turn on spawn on this monument? [true/false]" : "Включить спавн на этом монументе? [true/false]")] public bool isEnabled { get; set; }
            [JsonProperty(en ? "Spawn Locations" : "Локации для спавна")] public HashSet<LocationConfig> locations { get; set; }
        }

        public class PerformanceConfig
        {
            [JsonProperty(en ? "Automatically apply the most optimized skins to buildings (It is not recommended to change!) [true/false]" : "Автоматически применять наиболее оптимизированные скины к постройкам (Не рекомендуется изменять!) [true/false]")] public bool autoSkin { get; set; }
            [JsonProperty(en ? "Remove the straw blocks at the beginning of the movement (It is not recommended to change!) [true/false]" : "Удалять соломенные блоки при начале движения (Не рекомендуется изменять!) [true/false]")] public bool killTwig { get; set; }
            [JsonProperty(en ? "Allow grade building in Wood [true/false]" : "Разрешить грейдить постройку в Дерево [true/false]")] public bool allowWood { get; set; }
            [JsonProperty(en ? "Allow grade building in Metal [true/false]" : "Разрешить грейдить постройку в Металл [true/false]")] public bool allowMetall { get; set; }
            [JsonProperty(en ? "Allow grade building in Stone (It is not recommended to change it!) [true/false]" : "Разрешить грейдить постройку в Камень (Не рекомендуется изменять!) [true/false]")] public bool allowStone { get; set; }
            [JsonProperty(en ? "Allow grade building in HQM (It is not recommended to change it!) [true/false]" : "Разрешить грейдить постройку в МВК (Не рекомендуется изменять!) [true/false]")] public bool allowHqm { get; set; }
            [JsonProperty(en ? "The number of building blocks updated per tick (It is not recommended to increase!)" : "Количество билдинг блоков, обновляемых за тик (Не рекомендуется увеличивать!)")] public int updatePerTick { get; set; }
        }

        public class BargeConfig
        {
            [JsonProperty(en ? "Preset name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty(en ? "Data file path" : "Имя дата-файла")] public string dataFileName { get; set; }
            [JsonProperty(en ? "The maximum number of floors (-1 is not limited)" : "Максимальное количество этажей (-1 не ограничивать)")] public int maxFlors { get; set; }
            [JsonProperty(en ? "Weight" : "Масса")] public int mass { get; set; }
            [JsonProperty(en ? "Engine (Cab must be installed)" : "Двигатель (Должна быть установлена кабина)")] public EngineConfig engineConfig { get; set; }
            [JsonProperty(en ? "Modules" : "Модули барж")] public HashSet<BargeModuleConfig> modules { get; set; }
            [JsonProperty(en ? "A flare for calling a barge" : "Флаер для вызова баржи")] public ItemConfig itemConfig { get; set; }
        }

        public class EngineConfig
        {
            [JsonProperty(en ? "Engine Power Multiplier" : "Множитель мощности двигателя")] public float powerScale { get; set; }
            [JsonProperty(en ? "Fuel consumption multiplier" : "Множитель потребления топлива")] public float fuelScale { get; set; }
            [JsonProperty(en ? "Turning speed Multiplier" : "Множитель скорости поворота")] public float rotateScale { get; set; }
        }

        public class BargeModuleConfig : PresetLocationConfig
        {
            [JsonProperty(en ? "Turn it on? [true/false]" : "Включить? [true/false]")] public bool isEnable { get; set; }
        }

        public class PresetLocationConfig : LocationConfig
        {
            [JsonProperty(en ? "Preset name" : "Название пресета")] public string presetName { get; set; }
        }

        public class LocationConfig
        {
            [JsonProperty(en ? "Position" : "Позиция")] public string position { get; set; }
            [JsonProperty(en ? "Rotation" : "Вращение")] public string rotation { get; set; }
        }

        public class ItemConfig
        {
            [JsonProperty("ShortName")] public string shortname { get; set; }
            [JsonProperty("SkinID (0 - default)")] public ulong skin { get; set; }
            [JsonProperty(en ? "Name (empty - default)" : "Название (empty - default)")] public string name { get; set; }
        }

        public class NotifyConfig : BaseMessageConfig
        {
            [JsonProperty(en ? "Discord setting (only for DiscordMessages plugin)" : "Настройка оповещений в Discord (только для тех, кто использует плагин Discord Messages)", Order = 100)] public DiscordMessagesConfig discordMessagesConfig { get; set; }
            [JsonProperty(en ? "Redefined messages" : "Переопределенные сообщения )", Order = 101)] public HashSet<RedefinedMessageConfig> redefinedMessages { get; set; }
        }

        public class RedefinedMessageConfig : BaseMessageConfig
        {
            [JsonProperty(en ? "Enable this message? [true/false]" : "Включить сообщение? [true/false]", Order = 1)] public bool isEnable { get; set; }
            [JsonProperty("Lang Key", Order = 1)] public string langKey { get; set; }
        }

        public class BaseMessageConfig
        {
            [JsonProperty(en ? "Chat Message setting" : "Настройки сообщений в чате", Order = 1)] public ChatConfig chatConfig { get; set; }
            [JsonProperty(en ? "Facepunch Game Tips setting" : "Настройка сообщений Facepunch Game Tip", Order = 2)] public GameTipConfig gameTipConfig { get; set; }
            [JsonProperty(en ? "GUI Announcements setting (only for GUIAnnouncements plugin)" : "Настройка GUI Announcements (только для тех, кто использует плагин GUI Announcements)", Order = 3)] public GUIAnnouncementsConfig guiAnnouncementsConfig { get; set; }
            [JsonProperty(en ? "Notify setting (only for Notify plugin)" : "Настройка Notify (только для тех, кто использует плагин Notify)", Order = 4)] public NotifyPluginConfig notifyPluginConfig { get; set; }
        }

        public class ChatConfig
        {
            [JsonProperty(en ? "Use chat notifications? [true/false]" : "Использовать ли чат? [true/false]")] public bool isEnabled { get; set; }
        }

        public class GameTipConfig
        {
            [JsonProperty(en ? "Use Facepunch Game Tips (notification bar above hotbar)? [true/false]" : "Использовать ли Facepunch Game Tip (оповещения над слотами быстрого доступа игрока)? [true/false]")] public bool isEnabled { get; set; }
            [JsonProperty(en ? "Style (0 - Blue Normal, 1 - Red Normal, 2 - Blue Long, 3 - Blue Short, 4 - Server Event)" : "Стиль (0 - Blue Normal, 1 - Red Normal, 2 - Blue Long, 3 - Blue Short, 4 - Server Event)")] public int style { get; set; }
        }

        public class GUIAnnouncementsConfig
        {
            [JsonProperty(en ? "Do you use GUI Announcements integration? [true/false]" : "Использовать ли GUI Announcements? [true/false]")] public bool isEnabled { get; set; }
            [JsonProperty(en ? "Banner color" : "Цвет баннера")] public string bannerColor { get; set; }
            [JsonProperty(en ? "Text color" : "Цвет текста")] public string textColor { get; set; }
            [JsonProperty(en ? "Adjust Vertical Position" : "Отступ от верхнего края")] public float apiAdjustVPosition { get; set; }
        }

        public class NotifyPluginConfig
        {
            [JsonProperty(en ? "Do you use Notify integration? [true/false]" : "Использовать ли Notify? [true/false]")] public bool isEnabled { get; set; }
            [JsonProperty(en ? "Type" : "Тип")] public int type { get; set; }
        }

        public class DiscordMessagesConfig
        {
            [JsonProperty(en ? "Do you use DiscordMessages? [true/false]" : "Использовать ли DiscordMessages? [true/false]")] public bool isEnabled { get; set; }
            [JsonProperty("Webhook URL")] public string webhookUrl { get; set; }
            [JsonProperty(en ? "Embed Color (DECIMAL)" : "Цвет полосы (DECIMAL)")] public int embedColor { get; set; }
            [JsonProperty(en ? "Keys of required messages" : "Ключи необходимых сообщений")] public HashSet<string> keys { get; set; }
        }

        private class PluginConfig
        {
            [JsonProperty(en ? "Version" : "Версия")] public VersionNumber version { get; set; }
            [JsonProperty(en ? "Chat Prefix" : "Префикс в чате")] public string prefix { get; set; }
            [JsonProperty(en ? "Spawn Config" : "Настройка спавна")] public SpawnConfig spawnConfig { get; set; }
            [JsonProperty(en ? "Performance Config" : "Настроки производительности")] public PerformanceConfig performanceConfig { get; set; }
            [JsonProperty(en ? "General Setting" : "Основные настройки")] public MainConfig mainConfig { get; set; }
            [JsonProperty(en ? "Presets of towed barges" : "Пресеты буксируемых барж")] public HashSet<BargeConfig> bargeConfigs { get; set; }
            [JsonProperty(en ? "Notification Settings" : "Настройки уведомлений")] public NotifyConfig notifyConfig { get; set; }

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    version = new VersionNumber(1, 0, 6),
                    prefix = "[Barges]",
                    spawnConfig = new SpawnConfig
                    {
                        isSpawnEnabled = false,
                        maxBargeCount = 10,
                        minSpawnTime = 3600,
                        maxSpawnTime = 43200,
                        probabilities = new Dictionary<string, float>
                        {
                            ["rect_10x5"] = 5,
                            ["round_3"] = 5,
                            ["rect_5x5"] = 10,
                            ["rect_6x3"] = 10,
                            ["rect_3x3"] = 20,
                            ["rect_3x2"] = 20,
                            ["round_2"] = 20,
                        },
                        monuments = new HashSet<SpawnMonumentConfig>
                        {
                            new SpawnMonumentConfig
                            {
                                monumentName = "assets/bundled/prefabs/autospawn/monument/harbor/harbor_1.prefab",
                                isEnabled = true,
                                locations = new HashSet<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        position = "(115, 0, 170)",
                                        rotation = "(0, 0, 0)"
                                    }
                                }
                            },
                            new SpawnMonumentConfig
                            {
                                monumentName = "assets/bundled/prefabs/autospawn/monument/harbor/ferry_terminal_1.prefab",
                                isEnabled = true,
                                locations = new HashSet<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        position = "(100, 0, 145)",
                                        rotation = "(0, 0, 0)"
                                    }
                                }
                            },
                        }
                    },
                    performanceConfig = new PerformanceConfig
                    {
                        autoSkin = true,
                        killTwig = true,
                        allowWood = true,
                        allowMetall = true,
                        allowStone = false,
                        allowHqm = false,
                        updatePerTick = 15
                    },
                    mainConfig = new MainConfig
                    {
                        blockFishingVillage = true,
                        maxShoreDistance = 600,
                        bloskedItemShortnames = new HashSet<string>
                        {
                            "autoturret",
                            "samsite"
                        },
                        blockedCommands = new HashSet<string>
                        {
                            "home add",
                            "sethome"
                        }
                    },
                    bargeConfigs = new HashSet<BargeConfig>
                    {
                        new BargeConfig
                        {
                            presetName = "rect_10x5",
                            dataFileName = "rect_10x5",
                            maxFlors = 3,
                            mass = 17000,
                            engineConfig = new EngineConfig
                            {
                                fuelScale = 1.5f,
                                powerScale = 1,
                                rotateScale = 1f
                            },
                            modules = new HashSet<BargeModuleConfig>
                            {
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "ramp_1",
                                    position = "(0, 0, 15.7)",
                                    rotation = "(0, 0, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "cabine_1",
                                    position = "(-5.326, 0, 15.93)",
                                    rotation = "(0, 0, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "connector_1",
                                    position = "(0, 0, -15.93)",
                                    rotation = "(0, 180, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "anchor_1",
                                    position = "(-8.225, 1, 17)",
                                    rotation = "(0, 0, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "dock_1",
                                    position = "(-8.1, 0, 7)",
                                    rotation = "(0, 0, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "dock_1",
                                    position = "(8.1, 0, 7)",
                                    rotation = "(0, 180, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "dock_1",
                                    position = "(-8.1, 0, -7)",
                                    rotation = "(0, 0, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "dock_1",
                                    position = "(8.1, 0, -7)",
                                    rotation = "(0, 180, 0)"
                                }
                            },
                            itemConfig = new ItemConfig
                            {
                                shortname = "flare",
                                name = "Barge",
                                skin = 3358067991,
                            }
                        },
                        new BargeConfig
                        {
                            presetName = "rect_5x5",
                            dataFileName = "rect_5x5",
                            maxFlors = 4,
                            mass = 9000,
                            engineConfig = new EngineConfig
                            {
                                fuelScale = 1,
                                powerScale = 1,
                                rotateScale = 1f
                            },
                            modules = new HashSet<BargeModuleConfig>
                            {
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "connector_1",
                                    position = "(0, 0, 8.75)",
                                    rotation = "(0, 0, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "anchor_1",
                                    position = "(-8.225, 1, 8.225)",
                                    rotation = "(0, 0, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "dock_1",
                                    position = "(-8.1, 0, 0)",
                                    rotation = "(0, 0, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "dock_1",
                                    position = "(8.1, 0, 0)",
                                    rotation = "(0, 180, 0)"
                                }
                            },
                            itemConfig = new ItemConfig
                            {
                                shortname = "flare",
                                name = "Barge",
                                skin = 3358067724,
                            }
                        },
                        new BargeConfig
                        {
                            presetName = "rect_6x3",
                            dataFileName = "rect_6x3",
                            maxFlors = 4,
                            mass = 6000,
                            engineConfig = new EngineConfig
                            {
                                fuelScale = 1,
                                powerScale = 1f,
                                rotateScale = 1f
                            },
                            modules = new HashSet<BargeModuleConfig>
                            {
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "cabine_1",
                                    position = "(-3, 0, 10.12)",
                                    rotation = "(0, 0, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "ramp_1",
                                    position = "(0, 0, -9.5)",
                                    rotation = "(0, 180, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "anchor_1",
                                    position = "(-5.35, 1, 9.8)",
                                    rotation = "(0, 0, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "dock_1",
                                    position = "(5.1, 0, 0)",
                                    rotation = "(0, 180, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "dock_1",
                                    position = "(-5.1, 0, 0)",
                                    rotation = "(0, 0, 0)"
                                }
                            },
                            itemConfig = new ItemConfig
                            {
                                shortname = "flare",
                                name = "Barge",
                                skin = 3358067847,
                            }
                        },
                        new BargeConfig
                        {
                            presetName = "rect_3x3",
                            dataFileName = "rect_3x3",
                            maxFlors = 4,
                            mass = 3000,
                            engineConfig = new EngineConfig
                            {
                                fuelScale = 1,
                                powerScale = 1,
                                rotateScale = 1f
                            },
                            modules = new HashSet<BargeModuleConfig>
                            {
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "connector_1",
                                    position = "(0, 0, 5.55)",
                                    rotation = "(0, 0, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "anchor_1",
                                    position = "(-5.35, 1, 5.3)",
                                    rotation = "(0, 0, 0)"
                                }
                            },
                            itemConfig = new ItemConfig
                            {
                                shortname = "flare",
                                name = "Barge",
                                skin = 3358064514,
                            }
                        },
                        new BargeConfig
                        {
                            presetName = "rect_3x2",
                            dataFileName = "rect_3x2",
                            maxFlors = 3,
                            mass = 2000,
                            itemConfig = new ItemConfig
                            {
                                shortname = "flare",
                                name = "Barge",
                                skin = 3358106980,
                            },
                            engineConfig = new EngineConfig
                            {
                                fuelScale = 0.5f,
                                powerScale = 1f,
                                rotateScale = 1f
                            },
                            modules = new HashSet<BargeModuleConfig>
                            {
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "cabine_1",
                                    position = "(0, 0, 5.2)",
                                    rotation = "(0, 0, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "anchor_1",
                                    position = "(-3.5, 1, 5.3)",
                                    rotation = "(0, 0, 0)"
                                },
                            },
                        },
                        new BargeConfig
                        {
                            presetName = "round_2",
                            dataFileName = "round_2",
                            maxFlors = 5,
                            mass = 7000,
                            engineConfig = new EngineConfig
                            {
                                fuelScale = 1,
                                powerScale = 1,
                                rotateScale = 1f
                            },
                            modules = new HashSet<BargeModuleConfig>
                            {
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "connector_1",
                                    position = "(0, 0, 6.2)",
                                    rotation = "(0, 0, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "anchor_1",
                                    position = "(-3.6, 1, 6)",
                                    rotation = "(0, 0, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "dock_1",
                                    position = "(-5.105, 0, 2.924)",
                                    rotation = "(0, 30, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "dock_1",
                                    position = "(5.105, 0, 2.924)",
                                    rotation = "(0, 150, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "dock_1",
                                    position = "(5.105, 0, -2.924)",
                                    rotation = "(0, 210, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "dock_1",
                                    position = "(-5.105, 0, -2.924)",
                                    rotation = "(0, 330, 0)"
                                }
                            },
                            itemConfig = new ItemConfig
                            {
                                shortname = "flare",
                                name = "Barge",
                                skin = 3358068089,
                            }
                        },
                        new BargeConfig
                        {
                            presetName = "round_3",
                            dataFileName = "round_3",
                            maxFlors = 6,
                            mass = 12000,
                            engineConfig = new EngineConfig
                            {
                                fuelScale = 1,
                                powerScale = 1,
                                rotateScale = 1f
                            },
                            modules = new HashSet<BargeModuleConfig>
                            {
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "connector_1",
                                    position = "(0, 0, 9)",
                                    rotation = "(0, 0, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "anchor_1",
                                    position = "(-4.5, 1, 8.5)",
                                    rotation = "(0, 0, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "dock_1",
                                    position = "(-7.444, 0, -4.106)",
                                    rotation = "(0, 330, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "dock_1",
                                    position = "(-7.315, 0, 4.248)",
                                    rotation = "(0, 30, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "dock_1",
                                    position = "(7.376, 0, 4.186)",
                                    rotation = "(0, 150, 0)"
                                },
                                new BargeModuleConfig
                                {
                                    isEnable = true,
                                    presetName = "dock_1",
                                    position = "(7.32, 0, -4.25)",
                                    rotation = "(0, 210, 0)"
                                },
                            },
                            itemConfig = new ItemConfig
                            {
                                shortname = "flare",
                                name = "Barge",
                                skin = 3358068268,
                            }
                        },
                    },
                    notifyConfig = new NotifyConfig
                    {
                        chatConfig = new ChatConfig
                        {
                            isEnabled = false,
                        },
                        gameTipConfig = new GameTipConfig
                        {
                            isEnabled = true,
                            style = 1,
                        },
                        guiAnnouncementsConfig = new GUIAnnouncementsConfig
                        {
                            isEnabled = false,
                            bannerColor = "Grey",
                            textColor = "White",
                            apiAdjustVPosition = 0.03f
                        },
                        notifyPluginConfig = new NotifyPluginConfig
                        {
                            isEnabled = false,
                            type = 0
                        },
                        discordMessagesConfig = new DiscordMessagesConfig
                        {
                            isEnabled = false,
                            webhookUrl = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
                            embedColor = 13516583,
                            keys = new HashSet<string>
                            {
                            }
                        },
                        redefinedMessages = new HashSet<RedefinedMessageConfig>
                        {
                            new RedefinedMessageConfig
                            {
                                isEnable = true,
                                langKey = "GotBarge",
                                chatConfig = new ChatConfig
                                {
                                    isEnabled = false,
                                },
                                gameTipConfig = new GameTipConfig
                                {
                                    isEnabled = true,
                                    style = 2,
                                },
                                guiAnnouncementsConfig = new GUIAnnouncementsConfig
                                {
                                    isEnabled = false,
                                    bannerColor = "Grey",
                                    textColor = "White",
                                    apiAdjustVPosition = 0.03f
                                },
                                notifyPluginConfig = new NotifyPluginConfig
                                {
                                    isEnabled = false,
                                    type = 0
                                },
                            }
                        }
                    },
                };
            }
        }
        #endregion Config
    }
}

namespace Oxide.Plugins.BargesExtensionMethods
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

            using (var enumerator = source.GetEnumerator())
                while (enumerator.MoveNext())
                    if (predicate(enumerator.Current))
                        result.Add(enumerator.Current);

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

        public static bool IsExists(this BaseNetworkable entity) => entity != null && !entity.IsDestroyed;

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

        public static HashSet<T> OfType<T>(this IEnumerable<BaseNetworkable> source)
        {
            HashSet<T> result = new HashSet<T>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (enumerator.Current is T) result.Add((T)(object)enumerator.Current);
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

        public static bool IsEqualVector3(this Vector3 a, Vector3 b) => Vector3.Distance(a, b) < 0.1f;

        public static List<TSource> OrderByQuickSort<TSource>(this List<TSource> source, Func<TSource, float> predicate)
        {
            return source.QuickSort(predicate, 0, source.Count - 1);
        }

        private static List<TSource> QuickSort<TSource>(this List<TSource> source, Func<TSource, float> predicate, int minIndex, int maxIndex)
        {
            if (minIndex >= maxIndex) return source;

            int pivotIndex = minIndex - 1;
            for (int i = minIndex; i < maxIndex; i++)
            {
                if (predicate(source[i]) < predicate(source[maxIndex]))
                {
                    pivotIndex++;
                    source.Replace(pivotIndex, i);
                }
            }
            pivotIndex++;
            source.Replace(pivotIndex, maxIndex);

            QuickSort(source, predicate, minIndex, pivotIndex - 1);
            QuickSort(source, predicate, pivotIndex + 1, maxIndex);

            return source;
        }

        private static void Replace<TSource>(this IList<TSource> source, int x, int y)
        {
            TSource t = source[x];
            source[x] = source[y];
            source[y] = t;
        }

        public static object GetPrivateFieldValue(this object obj, string fieldName)
        {
            FieldInfo fi = GetPrivateFieldInfo(obj.GetType(), fieldName);
            if (fi != null) return fi.GetValue(obj);
            else return null;
        }

        public static void SetPrivateFieldValue(this object obj, string fieldName, object value)
        {
            FieldInfo info = GetPrivateFieldInfo(obj.GetType(), fieldName);
            if (info != null) info.SetValue(obj, value);
        }

        public static FieldInfo GetPrivateFieldInfo(Type type, string fieldName)
        {
            foreach (FieldInfo fi in type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance)) if (fi.Name == fieldName) return fi;
            return null;
        }

        public static Action GetPrivateAction(this object obj, string methodName)
        {
            MethodInfo mi = obj.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (mi != null) return (Action)Delegate.CreateDelegate(typeof(Action), obj, mi);
            else return null;
        }

        public static object CallPrivateMethod(this object obj, string methodName, params object[] args)
        {
            MethodInfo mi = obj.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (mi != null) return mi.Invoke(obj, args);
            else return null;
        }
    }
}