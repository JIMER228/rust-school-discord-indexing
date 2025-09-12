using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Plugins.BuildingSitesExtensionMethods;
using Newtonsoft.Json;
using CompanionServer.Handlers;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BuildingSites", "Forum: https://topplugin.ru Ds: alone_sempai Vk: https://vk.com/rustnastroika", "1.0.7")]
    class BuildingSites : RustPlugin
    {
        #region Variables
        static BuildingSites ins;
        const bool en = false;
        HashSet<string> subscribeMethods = new HashSet<string>
        {
            "CanBuild",
            "OnEntitySpawned",
            "OnExplosiveThrown",
            "OnExplosiveDropped",
            "OnActiveItemChanged",
            "CanLootEntity",
            "OnEntityTakeDamage",
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
            LoadDefaultMessages();

            if (!TryLoadData())
            {
                NotifyManagerLite.PrintError(null, "DataNotFound_Exeption");
                NextTick(() => Interface.Oxide.UnloadPlugin(Name));
                return;
            }

            BuildingSite.Initialize();
            PlayerSiteSpawner.StartUpdate();
            PermissionManager.RegisterPermissions();
            Subscribes();
        }

        void Unload()
        {
            BuildingSite.UnloadSites();
            SiteSpawner.StopAutoSpawn();
            PlayerSiteSpawner.StopUpdate();
            Ch47Killer.Unload();
            ins = null;
        }

        object CanBuild(Planner planner, Construction prefab, Construction.Target constructionTarget)
        {
            if (!_config.skyTypeConfig.isTurretsDisable && !_config.skyTypeConfig.isSamsiteDisable)
                return null;

            BasePlayer player = planner.GetOwnerPlayer();
            Item item = planner.GetItem();

            if (item == null || player == null)
                return null;

            if ((_config.skyTypeConfig.isTurretsDisable && item.info.shortname == "autoturret") || (_config.skyTypeConfig.isSamsiteDisable && item.info.shortname == "samsite"))
            {
                float distance;

                BuildingSite buildingSite = BuildingSite.GetClosestSite<SkySiteConfig>(constructionTarget.position, out distance);
                if (buildingSite == null)
                    return null;

                if (distance < buildingSite.siteConfig.outsideRadius * 1.5f)
                {
                    NotifyManagerLite.SendMessageToPlayer(player, "BlockedOnSite");
                    return true;
                }
            }

            return null;
        }

        void OnEntitySpawned(BuildingPrivlidge buildingPrivlidge)
        {
            if (buildingPrivlidge == null)
                return;

            NextTick(() =>
            {
                BuildingSite buildingSite = BuildingSite.GetSiteByBuildingPrivilege(buildingPrivlidge);

                if (buildingSite != null)
                    buildingSite.DeleteMapMarker();
            });
        }

        void OnEntitySpawned(CH47HelicopterAIController ch47)
        {
            if (ch47 == null)
                return;

            Ch47Killer.AttachClass(ch47);
        }

        void OnExplosiveThrown(BasePlayer player, RoadFlare roadFlare, ThrownWeapon thrownWeapon)
        {
            OnPlayerDropFlare(player, roadFlare, thrownWeapon);
        }

        void OnExplosiveDropped(BasePlayer player, RoadFlare roadFlare, ThrownWeapon thrownWeapon)
        {
            OnPlayerDropFlare(player, roadFlare, thrownWeapon);
        }

        void OnPlayerDropFlare(BasePlayer player, RoadFlare roadFlare, ThrownWeapon thrownWeapon)
        {
            if (!player.IsRealPlayer() || roadFlare == null || thrownWeapon == null)
                return;

            Item item = thrownWeapon.GetItem();
            if (item == null)
                return;

            BaseSiteConfig baseSiteConfig = BuildingSite.GetSiteConfigBySkinId(item.skin);
            if (baseSiteConfig == null)
                return;

            SiteSpawnFlare.Attach(roadFlare, player, baseSiteConfig);
        }

        void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (!player.IsRealPlayer() || newItem == null)
                return;

            if (newItem != null && newItem.info.shortname == "flare")
            {
                BaseSiteConfig baseSiteConfig = BuildingSite.GetSiteConfigBySkinId(newItem.skin);
                if (baseSiteConfig == null)
                    return;

                BuildingSiteData siteData;
                ins.siteCustomizationDatas.TryGetValue(baseSiteConfig.dataFileName, out siteData);
                if (siteData == null)
                    return;

                PlayerSiteSpawner.AddPlayer(player, baseSiteConfig);

                GroundSiteConfig groundSiteConfig = baseSiteConfig as GroundSiteConfig;
                if (groundSiteConfig != null)
                {
                    NotifyManagerLite.SendMessageToPlayer(player, "GroundFlare_Description", _config.prefix);
                    return;
                }

                WaterSiteConfig waterSiteConfig = baseSiteConfig as WaterSiteConfig;
                if (waterSiteConfig != null)
                {
                    NotifyManagerLite.SendMessageToPlayer(player, "WaterFlare_Description", _config.prefix);
                    return;
                }

                SkySiteConfig skySiteConfig = baseSiteConfig as SkySiteConfig;
                if (skySiteConfig != null)
                {
                    NotifyManagerLite.SendMessageToPlayer(player, "SkyFlare_Description", _config.prefix);
                    return;
                }
            }
        }

        object CanLootEntity(BasePlayer player, StorageContainer storageContainer)
        {
            if (player == null || storageContainer == null)
                return null;

            if (!ins._config.mainConfig.onlyAutedLootPumpjack)
                return null;

            if (storageContainer.ShortPrefabName == "fuelstorage" || storageContainer.ShortPrefabName == "crudeoutput")
            {
                MiningQuarry miningQuarry = storageContainer.GetParentEntity() as MiningQuarry;
                if (miningQuarry == null)
                    return null;

                DoorCloser doorCloser = miningQuarry.GetParentEntity() as DoorCloser;
                if (doorCloser == null)
                    return null;

                BuildingSite buildingSite = BuildingSite.GetSiteByEntity(doorCloser);
                if (buildingSite == null)
                    return null;

                BuildingPrivlidge buildingPrivlidge = miningQuarry.GetBuildingPrivilege();
                if (buildingPrivlidge == null)
                    buildingPrivlidge = doorCloser.GetBuildingPrivilege();

                if (buildingPrivlidge != null && !buildingPrivlidge.IsAuthed(player))
                {
                    NotifyManagerLite.SendMessageToPlayer(player, "Unauthorized");
                    return true;
                }
            }

            return null;
        }

        object OnEntityTakeDamage(BuildingBlock buildingBlock, HitInfo info)
        {
            if (buildingBlock == null || info == null)
                return null;

            if (!buildingBlock.HasParent())
                return null;

            BuildingSite buildingSite = BuildingSite.GetSiteByEntity(buildingBlock);
            if (buildingSite == null)
                return null;

            return true;
        }

        object CanEntityTakeDamage(BuildingBlock buildingBlock, HitInfo info)
        {
            if (buildingBlock == null || info == null)
                return null;

            if (!buildingBlock.HasParent())
                return null;

            BuildingSite buildingSite = BuildingSite.GetSiteByEntity(buildingBlock);
            if (buildingSite == null)
                return null;

            return false;
        }
        #endregion Hooks

        #region Methods
        void UpdateConfig()
        {
            if (_config.version != Version)
            {
                PluginConfig defaultConfig = PluginConfig.DefaultConfig();

                if (_config.version.Patch <= 0)
                {
                    if (_config.markerConfig.displayedName == "Свободное место под застройку")
                        _config.markerConfig.displayedName = defaultConfig.markerConfig.displayedName;

                    foreach (BaseSiteConfig baseSiteConfig in _config.groundTypeConfig.sites)
                        baseSiteConfig.permission = "";
                    foreach (BaseSiteConfig baseSiteConfig in _config.waterTypeConfig.sites)
                        baseSiteConfig.permission = "";
                    foreach (BaseSiteConfig baseSiteConfig in _config.skyTypeConfig.sites)
                        baseSiteConfig.permission = "";
                }
                if (_config.version.Patch <= 1)
                {
                    _config.mainConfig = defaultConfig.mainConfig;
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
        #endregion Methods

        #region Commands
        [ChatCommand("respawnsites")]
        void RespawnSitesChatCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin)
                return;

            SiteSpawner.StartAutoSpawn();
            NotifyManagerLite.SendMessageToPlayer(player, "Spawn_Start");
        }

        [ConsoleCommand("spawnsite")]
        void RespawnSitesConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                return;

            SiteSpawner.StartAutoSpawn();
            NotifyManagerLite.PrintLogMessage("Spawn_Start");
        }

        [ChatCommand("spawnsite")]
        void SpawnSiteChatCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin || arg.Length < 1)
                return;

            string presetName = arg[0];
            BaseSiteConfig siteConfig = BuildingSite.GetSiteConfigByPresetName(presetName);
            if (siteConfig == null)
            {
                NotifyManagerLite.PrintError(player, "ConfigNotFound_Exeption", presetName);
                return;
            }

            SiteSpawner.SpawnSiteInRandomLocation(presetName);
            NotifyManagerLite.SendMessageToPlayer(player, "Spawn_Start");
        }

        [ConsoleCommand("spawnsite")]
        void SpawnSiteConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null || arg.Args.Length < 1)
                return;

            string presetName = arg.Args[0];
            BaseSiteConfig siteConfig = BuildingSite.GetSiteConfigByPresetName(presetName);
            if (siteConfig == null)
            {
                NotifyManagerLite.PrintError(null, "ConfigNotFound_Exeption", presetName);
                return;
            }

            SiteSpawner.SpawnSiteInRandomLocation(presetName);
            NotifyManagerLite.PrintLogMessage("Spawn_Start");
        }

        [ChatCommand("spawnsitemypos")]
        void SpawnSitePlayerPosChatCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin)
                return;

            string presetName = arg[0];
            BaseSiteConfig siteConfig = BuildingSite.GetSiteConfigByPresetName(presetName);
            if (siteConfig == null)
            {
                NotifyManagerLite.PrintError(player, "ConfigNotFound_Exeption", presetName);
                return;
            }

            BuildingSite.SpawnSite(presetName, player.transform.position, Quaternion.identity);
        }

        [ChatCommand("givesite")]
        void GiveSiteChatCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin || arg.Length < 1)
                return;

            string presetName = arg[0];
            BaseSiteConfig siteConfig = BuildingSite.GetSiteConfigByPresetName(presetName);
            if (siteConfig == null)
            {
                NotifyManagerLite.PrintError(player, "ConfigNotFound_Exeption", presetName);
                return;
            }

            LootManager.GiveItemToPLayer(player, siteConfig.itemConfig, 1);
        }

        [ConsoleCommand("givesite")]
        void GiveSiteConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null || arg.Args.Length < 2)
                return;

            string presetName = arg.Args[0];
            BaseSiteConfig siteConfig = BuildingSite.GetSiteConfigByPresetName(presetName);
            if (siteConfig == null)
            {
                NotifyManagerLite.PrintError(null, "ConfigNotFound_Exeption", presetName);
                return;
            }

            ulong targetUserId = Convert.ToUInt64(arg.Args[1]);
            BasePlayer targetPlayer = BasePlayer.FindByID(targetUserId);
            if (targetPlayer == null)
            {
                NotifyManagerLite.PrintError(null, "PlayerNotFound_Exeption", arg.Args[1]);
                return;
            }

            LootManager.GiveItemToPLayer(targetPlayer, siteConfig.itemConfig, 1);
            NotifyManagerLite.SendMessageToPlayer(targetPlayer, "GotSite");
        }

        [ConsoleCommand("killallsites")]
        void KillAllSitesCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null)
                return;

            BuildingSite.KillAllSites();
        }

        [ChatCommand("killallsites")]
        void KillAllSitesChatCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin)
                return;

            BuildingSite.KillAllSites();
        }


        [ChatCommand("killsite")]
        void KillSiteCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin)
                return;

            BaseEntity target = PositionDefiner.RaycastAll<BaseEntity>(player.eyes.HeadRay());
            if (target == null)
                return;

            BuildingSite buildingSite = BuildingSite.GetSiteByEntity(target);
            if (buildingSite == null)
                return;

            buildingSite.KillSite();
        }

        [ChatCommand("savemapsite")]
        void SaveMapCommand(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin)
                return;

            string locationName = arg[0];

            MapSaver.SaveMap(locationName);
        }
        #endregion Commands

        #region Classes
        static class PlayerSiteSpawner
        {
            static HashSet<PlayerFlareInfo> playersWithFlare = new HashSet<PlayerFlareInfo>();
            static Coroutine updateCorountine;

            internal static void AddPlayer(BasePlayer player, BaseSiteConfig baseSiteConfig)
            {
                if (playersWithFlare.Any(x => x.player != null && x.player.userID == player.userID))
                    return;

                PlayerFlareInfo playerFlareData = new PlayerFlareInfo(player, baseSiteConfig);
                playersWithFlare.Add(playerFlareData);
            }

            internal static void StartUpdate()
            {
                updateCorountine = ServerMgr.Instance.StartCoroutine(SpawnLocationCoroutine());
            }

            internal static void StopUpdate()
            {
                if (updateCorountine != null)
                    ServerMgr.Instance.StopCoroutine(updateCorountine);
            }

            static IEnumerator SpawnLocationCoroutine()
            {
                while (true)
                {
                    playersWithFlare.RemoveWhere(x => !IsFlareDataActive(x));

                    foreach (PlayerFlareInfo playerFlareData in playersWithFlare)
                    {
                        DisplayPlayerData(playerFlareData);
                    }

                    yield return CoroutineEx.waitForSeconds(1f);
                }
            }

            static void DisplayPlayerData(PlayerFlareInfo playerFlareData)
            {
                if (!string.IsNullOrEmpty(playerFlareData.siteConfig.permission) && !PermissionManager.IsUserHavePermission(playerFlareData.player.UserIDString, playerFlareData.siteConfig.permission))
                {
                    NotifyManagerLite.SendMessageToPlayer(playerFlareData.player, "NoPermission");
                    return;
                }

                HashSet<BaseEntity> entities = new HashSet<BaseEntity>();
                Vector3 spawnPosition = playerFlareData.player.transform.position;
                bool isPlacesuitable = SiteSpawner.IsPositionAvilable(ref spawnPosition, playerFlareData.siteConfig, out entities, true);

                if (isPlacesuitable)
                    NotifyManagerLite.SendMessageToPlayer(playerFlareData.player, "Position_Suitable");
                else
                    NotifyManagerLite.SendMessageToPlayer(playerFlareData.player, "Position_NotSuitable");
            }

            static bool IsFlareDataActive(PlayerFlareInfo playerFlareData)
            {
                if (playerFlareData.player == null || playerFlareData.player.IsSleeping())
                    return false;

                Item activeItem = playerFlareData.player.GetActiveItem();
                if (activeItem == null || activeItem.info.shortname != "flare")
                    return false;

                return true;
            }
        }

        class SiteSpawnFlare : FacepunchBehaviour
        {
            RoadFlare roadFlare;
            PlayerFlareInfo playerFlareData;

            internal static SiteSpawnFlare Attach(RoadFlare roadFlare, BasePlayer player, BaseSiteConfig siteConfig)
            {
                SiteSpawnFlare siteSpawnFlare = roadFlare.gameObject.AddComponent<SiteSpawnFlare>();
                siteSpawnFlare.Init(roadFlare, player, siteConfig);
                return siteSpawnFlare;
            }

            void Init(RoadFlare roadFlare, BasePlayer player, BaseSiteConfig siteConfig)
            {
                playerFlareData = new PlayerFlareInfo(player, siteConfig);
                this.roadFlare = roadFlare;
                roadFlare.enableSaving = false;
                roadFlare.waterCausesExplosion = false;

                if (!string.IsNullOrEmpty(siteConfig.permission) && !PermissionManager.IsUserHavePermission(player.UserIDString, siteConfig.permission))
                {
                    LootManager.GiveItemToPLayer(player, playerFlareData.siteConfig.itemConfig, 1);
                    NotifyManagerLite.SendMessageToPlayer(player, "NoPermission");
                    roadFlare.Kill();
                    return;
                }

                roadFlare.Invoke(CallSite, siteConfig is WaterSiteConfig ? 30f : 15f);
                SphereEntity sphereEntity = BuildManager.SpawnChildEntity(roadFlare, "assets/bundled/prefabs/modding/events/twitch/br_sphere_red.prefab", Vector3.zero, Vector3.zero, isDecor: false) as SphereEntity;
                sphereEntity.LerpRadiusTo(100, float.MaxValue);

                if (siteConfig is WaterSiteConfig)
                    roadFlare.InvokeRepeating(WaterCheck, 1f, 1f);
            }

            void OnCollisionEnter(Collision collision)
            {
                Rigidbody rigidbody = roadFlare.GetComponent<Rigidbody>();
                rigidbody.isKinematic = true;
            }

            void WaterCheck()
            {
                if (roadFlare.transform.position.y <= 0)
                {
                    Rigidbody rigidbody = roadFlare.GetComponent<Rigidbody>();
                    rigidbody.isKinematic = true;
                    roadFlare.transform.position = new Vector3(roadFlare.transform.position.x, 0, roadFlare.transform.position.z);
                    roadFlare.CancelInvoke(WaterCheck);
                }
            }

            void CallSite()
            {
                if (ins._config.mainConfig.maxLocationNumberPerPlayer >= 0)
                {
                    HashSet<BuildingSite> playerSites = BuildingSite.GetPlayersSites(playerFlareData.player.userID);
                    int playerSitesCount = playerSites.Count;

                    if (playerSitesCount >= ins._config.mainConfig.maxLocationNumberPerPlayer)
                    {
                        LootManager.GiveItemToPLayer(playerFlareData.player, playerFlareData.siteConfig.itemConfig, 1);
                        NotifyManagerLite.SendMessageToPlayer(playerFlareData.player, "MaxSitesForPlayer");
                        roadFlare.Kill();
                        return;
                    }
                }

                BuildingSite buildingSite = SiteSpawner.TrySpawnSite(this.transform.position, playerFlareData.siteConfig);
                if (buildingSite == null)
                {
                    LootManager.GiveItemToPLayer(playerFlareData.player, playerFlareData.siteConfig.itemConfig, 1);
                    NotifyManagerLite.SendMessageToPlayer(playerFlareData.player, "Spawn_Failed");
                }
                else
                {
                    buildingSite.SetSiteOwner(playerFlareData.player.userID);
                }

                roadFlare.Kill();
            }
        }

        class PlayerFlareInfo
        {
            internal BasePlayer player;
            internal BaseSiteConfig siteConfig;

            internal PlayerFlareInfo(BasePlayer player, BaseSiteConfig siteConfig)
            {
                this.player = player;
                this.siteConfig = siteConfig;
            }
        }

        static class SiteSpawner
        {
            static Coroutine spawnCorountine;

            internal static void StartAutoSpawn()
            {
                if (!ins._config.groundTypeConfig.isAutoSpawn && !ins._config.waterTypeConfig.isAutoSpawn && !ins._config.skyTypeConfig.isAutoSpawn)
                    return;

                spawnCorountine = ServerMgr.Instance.StartCoroutine(AutoSpawnCoroutine());
            }

            static IEnumerator AutoSpawnCoroutine()
            {
                NotifyManagerLite.PrintWarningMessage("SpawnStart_Log");

                int groundSitesCount = UnityEngine.Random.Range(ins._config.groundTypeConfig.minAmount, ins._config.groundTypeConfig.maxAmount);
                if (ins._config.groundTypeConfig.isAutoSpawn && groundSitesCount > 0)
                {
                    for (int i = 0; i < groundSitesCount; i++)
                    {
                        BaseSiteConfig siteConfig = GetRandomSiteConfig(ins._config.groundTypeConfig.sites);
                        yield return SpawnLocationCoroutine(siteConfig);
                    }
                }

                int waterSitesCount = UnityEngine.Random.Range(ins._config.waterTypeConfig.minAmount, ins._config.waterTypeConfig.maxAmount);
                if (ins._config.waterTypeConfig.isAutoSpawn && waterSitesCount > 0)
                {
                    for (int i = 0; i < waterSitesCount; i++)
                    {
                        BaseSiteConfig siteConfig = GetRandomSiteConfig(ins._config.waterTypeConfig.sites);
                        yield return SpawnLocationCoroutine(siteConfig);
                    }
                }

                int skySitesCount = UnityEngine.Random.Range(ins._config.skyTypeConfig.minAmount, ins._config.skyTypeConfig.maxAmount);
                if (ins._config.skyTypeConfig.isAutoSpawn && skySitesCount > 0)
                {
                    for (int i = 0; i < skySitesCount; i++)
                    {
                        BaseSiteConfig siteConfig = GetRandomSiteConfig(ins._config.skyTypeConfig.sites);
                        yield return SpawnLocationCoroutine(siteConfig);
                    }
                }

                NotifyManagerLite.PrintWarningMessage("SpawnStop_Log");

                yield return CoroutineEx.waitForSeconds(1);
            }

            static BaseSiteConfig GetRandomSiteConfig(IEnumerable<BaseSiteConfig> siteConfigs)
            {
                float sumChance = 0;
                foreach (BaseSiteConfig baseSiteConfig in siteConfigs)
                    sumChance += baseSiteConfig.probability;

                float random = UnityEngine.Random.Range(0, sumChance);

                foreach (BaseSiteConfig baseSiteConfig in siteConfigs)
                {
                    random -= baseSiteConfig.probability;

                    if (random <= 0)
                        return baseSiteConfig;
                }

                return null;
            }

            internal static void StopAutoSpawn()
            {
                if (spawnCorountine != null)
                    ServerMgr.Instance.StopCoroutine(spawnCorountine);
            }

            internal static void SpawnSiteInRandomLocation(string presetName)
            {
                BaseSiteConfig baseSiteConfig = BuildingSite.GetSiteConfigByPresetName(presetName);
                if (baseSiteConfig == null)
                {
                    NotifyManagerLite.PrintError(null, "ConfigNotFound_Exeption", presetName);
                    return;
                }

                BuildingSiteData siteData;
                ins.siteCustomizationDatas.TryGetValue(baseSiteConfig.dataFileName, out siteData);
                if (siteData == null)
                {
                    NotifyManagerLite.PrintError(null, "DataFileNotFound_Exeption", baseSiteConfig.dataFileName);
                    return;
                }

                ServerMgr.Instance.StartCoroutine(SpawnLocationCoroutine(baseSiteConfig));
            }

            static IEnumerator SpawnLocationCoroutine(BaseSiteConfig siteConfig)
            {
                int counter = 5000;
                bool isSpawned = false;

                while (counter-- > 0)
                {
                    Vector3 position = PositionDefiner.GetRandomMapPoint();

                    if (TrySpawnSite(position, siteConfig) != null)
                        isSpawned = true;

                    if (isSpawned)
                        break;
                    else if (counter % 3 == 0)
                        yield return CoroutineEx.waitForEndOfFrame;
                }

                if (!isSpawned)
                    NotifyManagerLite.PrintError(null, $"FailedToSpawn {siteConfig.presetName}");

                yield return CoroutineEx.waitForSeconds(3f);
            }

            internal static BuildingSite TrySpawnSite(Vector3 position, BaseSiteConfig siteConfig)
            {
                HashSet<BaseEntity> entitiesForDestroy;

                if (IsPositionAvilable(ref position, siteConfig, out entitiesForDestroy))
                {
                    return SpawnSite(siteConfig.presetName, position, entitiesForDestroy);
                }

                return null;
            }

            internal static bool IsPositionAvilable(ref Vector3 position, BaseSiteConfig siteConfig, out HashSet<BaseEntity> entitiesForDestroy, bool ignorePlayers = false)
            {
                entitiesForDestroy = new HashSet<BaseEntity>();

                GroundSiteConfig groundSiteConfig = siteConfig as GroundSiteConfig;
                if (groundSiteConfig != null)
                {
                    if (IsGroundPositionAvailable(position, groundSiteConfig, out entitiesForDestroy, ignorePlayers))
                        return true;

                    return false;
                }

                WaterSiteConfig waterSiteConfig = siteConfig as WaterSiteConfig;
                if (waterSiteConfig != null)
                {
                    position.y = 0;

                    if (IsWaterPositionAvailable(position, waterSiteConfig, out entitiesForDestroy, ignorePlayers))
                        return true;

                    return false;
                }

                SkySiteConfig skySiteConfig = siteConfig as SkySiteConfig;
                if (skySiteConfig != null)
                {
                    position.y = UnityEngine.Random.Range(ins._config.skyTypeConfig.minSpawnHeight, ins._config.skyTypeConfig.maxSpawnHeight);

                    if (IsSkyPositionAvailable(position, skySiteConfig, ignorePlayers))
                        return true;

                    return false;
                }

                return false;
            }

            static BuildingSite SpawnSite(string sitePresetName, Vector3 position, HashSet<BaseEntity> entitiesForDestroy)
            {
                foreach (BaseEntity entity in entitiesForDestroy)
                    if (entity.IsExists())
                        entity.Kill();

                Quaternion randomRotation = Quaternion.Euler(0, UnityEngine.Random.Range(0, 360f), 0);
                return BuildingSite.SpawnSite(sitePresetName, position, randomRotation);
            }

            static bool IsGroundPositionAvailable(Vector3 position, GroundSiteConfig groundSiteConfig, out HashSet<BaseEntity> entitiesForDestroy, bool ignorePlayers)
            {
                entitiesForDestroy = new HashSet<BaseEntity>();

                float mapHeigth = TerrainMeta.HeightMap.GetHeight(position);
                if (mapHeigth < -ins._config.groundTypeConfig.maxDepht)
                    return false;

                if (BuildingSite.IsSpawnPositionBlockByOtherSite(position, groundSiteConfig))
                    return false;

                if (IsPositionBlockedByTopology(position, groundSiteConfig))
                    return false;

                if (IsPositionBlockByHeigthInRadius(position, groundSiteConfig.insideRadius, groundSiteConfig.maxUpDeltaHeigh, groundSiteConfig.maxDownDeltaHeigh))
                    return false;

                if (IsPositionBlockByHeigthInRadius(position, groundSiteConfig.insideRadius * 0.5f, groundSiteConfig.maxUpDeltaHeigh, groundSiteConfig.maxDownDeltaHeigh))
                    return false;

                if (IsAnyEntityBlockSpawn(position, groundSiteConfig, out entitiesForDestroy, ignorePlayers))
                    return false;

                return true;
            }

            static bool IsAnyEntityBlockSpawn(Vector3 position, BaseSiteConfig baseSiteConfig, out HashSet<BaseEntity> entitiesForDestroy, bool ignorePlayers)
            {
                entitiesForDestroy = new HashSet<BaseEntity>();

                foreach (Collider collider in UnityEngine.Physics.OverlapSphere(position, baseSiteConfig.outsideRadius))
                {
                    if (collider.name.Contains("heatSource"))
                        continue;

                    if (collider.name.Contains("Safe") || collider.name.Contains("Trigger (8)"))
                        return true;

                    BaseEntity entity = collider.ToBaseEntity();
                    if (entity == null)
                    {
                        string colliderLowerName = collider.name.ToLower();
                        if (colliderLowerName.Contains("prevent_building") || colliderLowerName.Contains("preventbuilding") || colliderLowerName.Contains("prevent building"))
                            return true;

                        continue;
                    }

                    if (entity.GetBuildingPrivilege() != null)
                        return true;

                    if (ignorePlayers && entity is BaseBoat && baseSiteConfig is WaterSiteConfig)
                        continue; 

                    if (entity is BuildingBlock or SimpleBuildingBlock or BaseVehicle)
                        return true;

                    if (!ignorePlayers && entity is BasePlayer)
                        return true;

                    if (entity is JunkPile or DiveSite or LootContainer or OreResourceEntity or BasePortal)
                        entitiesForDestroy.Add(entity);
                    else if (ins._config.mainConfig.killTrees && entity is TreeEntity)
                        entitiesForDestroy.Add(entity);
                }

                return false;
            }


            static bool IsPositionBlockedByTopology(Vector3 postition, BaseSiteConfig baseSiteConfig, int angleStep = 15)
            {
                if (TopologyChecker.IsPositionBlockedByTopologies(postition, baseSiteConfig))
                    return true;

                for (int angle = 0; angle < 360; angle += angleStep)
                {
                    float radian = 2f * Mathf.PI * angle / 360;
                    float x = postition.x + baseSiteConfig.outsideRadius * Mathf.Cos(radian);
                    float z = postition.z + baseSiteConfig.outsideRadius * Mathf.Sin(radian);
                    Vector3 positionInRadius = PositionDefiner.GetGroundPosition(new Vector3(x, postition.y, z));

                    if (TopologyChecker.IsPositionBlockedByTopologies(positionInRadius, baseSiteConfig))
                        return true;

                    if (baseSiteConfig is WaterSiteConfig && !TopologyChecker.IsOceanTopology(positionInRadius))
                        return true;
                }

                return false;
            }

            static bool IsPositionBlockByHeigthInRadius(Vector3 postition, float radius, float maxUpDelta, float maxDownDelta, int angleStep = 15)
            {
                for (int angle = 0; angle < 360; angle += angleStep)
                {
                    float radian = 2f * Mathf.PI * angle / 360;
                    float x = postition.x + radius * Mathf.Cos(radian);
                    float z = postition.z + radius * Mathf.Sin(radian);
                    Vector3 positionInRadius = PositionDefiner.GetGroundPosition(new Vector3(x, postition.y, z));
                    float delta = positionInRadius.y - postition.y;

                    if (delta > 0 && delta > maxUpDelta)
                        return true;

                    if (delta < 0 && -delta > maxDownDelta)
                        return true;
                }

                return false;
            }

            static bool IsWaterPositionAvailable(Vector3 position, WaterSiteConfig waterSiteConfig, out HashSet<BaseEntity> entitiesForDestroy, bool ignorePlayers)
            {
                entitiesForDestroy = new HashSet<BaseEntity>();

                float mapHeigth = TerrainMeta.HeightMap.GetHeight(position);
                if (-mapHeigth < ins._config.waterTypeConfig.minDepth)
                    return false;

                if (BuildingSite.IsSpawnPositionBlockByOtherSite(position, waterSiteConfig))
                    return false;

                if (GetShoreDistance(position) > ins._config.waterTypeConfig.maxShoreDistance)
                    return false;

                if (!IsPositionInMapBounds(position, waterSiteConfig))
                    return false;

                if (IsPositionBlockedByTopology(position, waterSiteConfig))
                    return false;

                if (PositionDefiner.IsPositionOnCargoPath(position))
                    return false;

                if (IsAnyEntityBlockSpawn(position, waterSiteConfig, out entitiesForDestroy, ignorePlayers))
                    return false;

                return true;
            }
           
            static float GetShoreDistance(Vector3 position)
            {
                float xDistanceToShore = position.x - World.Size / 2;
                float zDistanceToShore = position.z - World.Size / 2;
                float distanceToShore = xDistanceToShore > zDistanceToShore ? xDistanceToShore : zDistanceToShore; //Distance to shore = 32339
                return distanceToShore;
            }

            static bool IsSkyPositionAvailable(Vector3 position, SkySiteConfig skySiteConfig, bool ignorePlayers)
            {
                HashSet<BaseEntity> entitiesForDestroy = new HashSet<BaseEntity>();

                Vector3 groundPosition = PositionDefiner.GetGroundPosition(position);
                float distanceToGround = position.y - groundPosition.y;

                if (distanceToGround < ins._config.skyTypeConfig.minSpawnHeight / 2.5f)
                    return false;

                if (BuildingSite.IsSpawnPositionBlockByOtherSite(position, skySiteConfig))
                    return false;

                if (!IsPositionInMapBounds(position, skySiteConfig))
                    return false;

                if (IsAnyEntityBlockSpawn(position, skySiteConfig, out entitiesForDestroy, ignorePlayers))
                    return false;

                return true;
            }

            static bool IsPositionInMapBounds(Vector3 position, BaseSiteConfig baseSiteConfig)
            {
                if (Mathf.Abs(position.x) + baseSiteConfig.outsideRadius > World.Size / 2)
                    return false;

                if (Mathf.Abs(position.z) + baseSiteConfig.outsideRadius > World.Size / 2)
                    return false;

                return true;
            }
        }

        static class TopologyChecker
        {
            const int oceanTopologies = (int)(TerrainTopology.Enum.Ocean | TerrainTopology.Enum.Oceanside);
            const int blockedTopologies = (int)(TerrainTopology.Enum.Building | TerrainTopology.Enum.Road | TerrainTopology.Enum.Roadside | TerrainTopology.Enum.Rail | TerrainTopology.Enum.Railside);
            const int monumentTopologies = (int)(TerrainTopology.Enum.Monument | TerrainTopology.Enum.Building);
            const int beachTopologies = (int)(TerrainTopology.Enum.Beach | TerrainTopology.Enum.Beachside);
            const int riverTopologies = (int)(TerrainTopology.Enum.River | TerrainTopology.Enum.Riverside | TerrainTopology.Enum.Lake | TerrainTopology.Enum.Lakeside);

            internal static bool IsPositionBlockedByTopologies(Vector3 postition, BaseSiteConfig baseSiteConfig)
            {
                if (TopologyChecker.IsBlockedTopology(postition))
                    return true;

                if (!ins._config.groundTypeConfig.isRiverSpawnEnabled && TopologyChecker.IsRiverOrLakeTopology(postition))
                    return true;

                if (baseSiteConfig is GroundSiteConfig && !ins._config.groundTypeConfig.isBeachSpawnEnabled && (TopologyChecker.IsBeachTopology(postition) || TopologyChecker.IsOceanTopology(postition)))
                    return true;

                return false;
            }

            static bool IsBlockedTopology(Vector3 position)
            {
                int pointTopologies = TerrainMeta.TopologyMap.GetTopology(position);

                if ((pointTopologies & blockedTopologies) != 0)
                    return true;

                if ((pointTopologies & monumentTopologies) != 0)
                    return true;

                return false;
            }

            static bool IsBeachTopology(Vector3 position)
            {
                int pointTopologies = TerrainMeta.TopologyMap.GetTopology(position);

                if ((pointTopologies & beachTopologies) != 0)
                    return true;

                return false;
            }

            static bool IsRiverOrLakeTopology(Vector3 position)
            {
                int pointTopologies = TerrainMeta.TopologyMap.GetTopology(position);

                if ((pointTopologies & riverTopologies) != 0)
                    return true;

                return false;
            }

            internal static bool IsOceanTopology(Vector3 position)
            {
                int pointTopologies = TerrainMeta.TopologyMap.GetTopology(position);

                if ((pointTopologies & oceanTopologies) != 0)
                    return true;

                return false;
            }
        }

        class BuildingSite : FacepunchBehaviour
        {
            static HashSet<BuildingSite> buildingSites = new HashSet<BuildingSite>();
            internal BaseSiteConfig siteConfig;
            BaseEntity mainEntity;
            BuildingSiteData siteData;
            MapMarker mapMarker;
            HashSet<GameObject> shoudDestroyAfterUnload = new HashSet<GameObject>();

            internal static void Initialize()
            {
                LoadSites();

                if (buildingSites.Count == 0)
                    SiteSpawner.StartAutoSpawn();
            }

            static void LoadSites()
            {
                HashSet<DoorCloser> doorClosers = BaseNetworkable.serverEntities.OfType<DoorCloser>();

                foreach (DoorCloser doorCloser in doorClosers)
                {
                    if (doorCloser == null || doorCloser.net == null)
                        continue;

                    BaseSiteConfig siteConfig = GetSiteConfigBySkinId(doorCloser.skinID);
                    if (siteConfig == null)
                        continue;

                    BuildingSite buildingSite = TryAttachBuildingSiteClass(doorCloser, siteConfig.presetName, false);
                }
            }

            internal static BaseSiteConfig GetSiteConfigByPresetName(string presetName)
            {
                BaseSiteConfig result = null;

                result = ins._config.groundTypeConfig.sites.FirstOrDefault(x => x.presetName == presetName);

                if (result == null)
                    result = ins._config.waterTypeConfig.sites.FirstOrDefault(x => x.presetName == presetName);

                if (result == null)
                    result = ins._config.skyTypeConfig.sites.FirstOrDefault(x => x.presetName == presetName);

                return result;
            }

            internal static BaseSiteConfig GetSiteConfigBySkinId(ulong skinID)
            {
                BaseSiteConfig result = null;

                result = ins._config.groundTypeConfig.sites.FirstOrDefault(x => x.itemConfig.skin == skinID);

                if (result == null)
                    result = ins._config.waterTypeConfig.sites.FirstOrDefault(x => x.itemConfig.skin == skinID);

                if (result == null)
                    result = ins._config.skyTypeConfig.sites.FirstOrDefault(x => x.itemConfig.skin == skinID);

                return result;
            }

            internal static BuildingSite GetSiteByEntity(BaseEntity entity)
            {
                return buildingSites.FirstOrDefault(x => x != null && x.mainEntity.IsExists() && x.IsSiteEntity(entity));
            }

            internal static BuildingSite GetSiteByBuildingPrivilege(BuildingPrivlidge buildingPrivlidge)
            {
                foreach (BuildingSite buildingSite in buildingSites)
                {
                    if (buildingSite == null || buildingSite.mainEntity == null)
                        return null;

                    BuildingPrivlidge sitePrivilege = buildingSite.mainEntity.GetBuildingPrivilege();
                    if (sitePrivilege != null && sitePrivilege.net != null && sitePrivilege.net.ID.Value == buildingPrivlidge.net.ID.Value)
                        return buildingSite;

                    foreach (BaseEntity childEntity in buildingSite.mainEntity.children)
                    {
                        if (!childEntity.ShortPrefabName.Contains("admin"))
                            continue;

                        sitePrivilege = childEntity.GetBuildingPrivilege();
                        if (sitePrivilege != null && sitePrivilege.net != null && sitePrivilege.net.ID.Value == buildingPrivlidge.net.ID.Value)
                            return buildingSite;
                    }
                }

                return null;
            }

            internal static BuildingSite GetClosestSite<ConfigType>(Vector3 position, out float minDistance) where ConfigType : BaseSiteConfig
            {
                minDistance = float.MaxValue;
                BuildingSite result = null;

                foreach (BuildingSite buildingSite in buildingSites)
                {
                    if (buildingSite == null || buildingSite.siteConfig is not ConfigType)
                        continue;

                    Vector3 offset = buildingSite.siteData.offset.ToVector3();
                    float distance = Vector3.Distance(position, buildingSite.mainEntity.transform.position + offset);

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        result = buildingSite;
                    }
                }

                return result;
            }

            internal static HashSet<BuildingSite> GetPlayersSites(ulong userID)
            {
                return buildingSites.Where(x => x != null && x.mainEntity.IsExists() && x.mainEntity.OwnerID == userID);
            }

            internal static bool IsSpawnPositionBlockByOtherSite(Vector3 position, BaseSiteConfig siteConfig)
            {
                position.y = 0;

                foreach (BuildingSite site in buildingSites)
                {
                    if (site == null || !site.mainEntity.IsExists())
                        continue;

                    Vector3 siteGroundPosition = new Vector3(site.mainEntity.transform.position.x, 0, site.mainEntity.transform.position.z);
                    float distance = Vector3.Distance(position, siteGroundPosition);

                    if (distance < (siteConfig.outsideRadius + site.siteConfig.outsideRadius) * 1.5f)
                        return true;
                }

                return false;
            }

            internal static BuildingSite SpawnSite(string presetName, Vector3 position, Quaternion rotation)
            {
                BaseSiteConfig siteConfig = GetSiteConfigByPresetName(presetName);
                if (siteConfig == null)
                {
                    NotifyManagerLite.PrintError(null, "ConfigNotFound_Exeption", presetName);
                    return null;
                }

                BaseEntity mainEntity = BuildManager.SpawnRegularEntity("assets/prefabs/misc/doorcloser/doorcloser.prefab", position, rotation, siteConfig.itemConfig.skin, true);

                BuildingSite buildingSite = TryAttachBuildingSiteClass(mainEntity, presetName, true);
                if (buildingSite == null)
                    mainEntity.Kill();

                return buildingSite;
            }

            static BuildingSite TryAttachBuildingSiteClass(BaseEntity mainEntity, string presetName, bool firstSpawn)
            {
                BaseSiteConfig siteConfig = GetSiteConfigByPresetName(presetName);
                if (siteConfig == null)
                {
                    NotifyManagerLite.PrintError(null, "ConfigNotFound_Exeption", presetName);
                    return null;
                }

                BuildingSiteData siteData;
                ins.siteCustomizationDatas.TryGetValue(siteConfig.dataFileName, out siteData);
                if (siteData == null)
                {
                    NotifyManagerLite.PrintError(null, "DataFileNotFound_Exeption", siteConfig.dataFileName);
                    return null;
                }

                BuildingSite buildingSite = mainEntity.gameObject.AddComponent<BuildingSite>();
                buildingSites.Add(buildingSite);
                buildingSite.BuildSite(mainEntity, siteConfig, siteData, firstSpawn);
                return buildingSite;
            }

            void BuildSite(BaseEntity mainEntity, BaseSiteConfig siteConfig, BuildingSiteData siteData, bool firstSpawn)
            {
                this.mainEntity = mainEntity;
                this.siteConfig = siteConfig;
                this.siteData = siteData;

                Vector3 offset = siteData.offset.ToVector3();

                foreach (EntityData entityData in siteData.decorEntities)
                {
                    Vector3 localPosition = entityData.position.ToVector3() + offset;
                    Vector3 localRotation = entityData.rotation.ToVector3();

                    if (!firstSpawn && mainEntity.children.Any(x => x != null && x.PrefabName == entityData.prefabName && x.transform.localPosition == localPosition))
                        continue;

                    BaseEntity entity = BuildManager.SpawnChildEntity(mainEntity, entityData.prefabName, localPosition, localRotation, isDecor: true, enableSaving: false);

                    if (entity.ShortPrefabName == "coaling_tower_fuel_storage.entity" || entity.ShortPrefabName == "mailbox.deployed")
                        entity.SetFlag(BaseEntity.Flags.Busy, true);

                    if (entity.ShortPrefabName == "hotairballoon")
                    {
                        entity.SetFlag(BaseEntity.Flags.On, true);
                        entity.SetFlag(BaseEntity.Flags.Reserved11, true);
                        entity.SetFlag(BaseEntity.Flags.Reserved12, true);
                        entity.SetFlag(BaseEntity.Flags.Reserved13, true);
                        entity.SendNetworkUpdate();
                    }
                }

                foreach (BuildingBlockData buildingBlockData in siteData.buildingBlocks)
                {
                    Vector3 localPosition = buildingBlockData.position.ToVector3() + offset;
                    Vector3 localRotation = buildingBlockData.rotation.ToVector3();

                    BuildingBlock thisBuildingBlock = mainEntity.children.FirstOrDefault(x => x != null && x.PrefabName == buildingBlockData.prefabName && x.transform.localPosition == localPosition && x.transform.localEulerAngles == localRotation) as BuildingBlock;
                    if (thisBuildingBlock != null)
                    {
                        thisBuildingBlock.grounded = true;
                        continue;
                    }

                    BuildingBlock buildingBlock = BuildManager.SpawnChildBuildingBlock(buildingBlockData.prefabName, buildingBlockData.grade, buildingBlockData.color, buildingBlockData.skin, localPosition, localRotation, mainEntity);
                    buildingBlock.grounded = true;
                }

                foreach (EntityData regularEntityData in siteData.regularEntities)
                {
                    Vector3 localPosition = regularEntityData.position.ToVector3() + offset;
                    Vector3 localRotation = regularEntityData.rotation.ToVector3();

                    if (!firstSpawn && mainEntity.children.Any(x => x != null && x.PrefabName == regularEntityData.prefabName && Vector3.Distance(x.transform.localPosition, localPosition) < 0.1f))
                        continue;

                    BaseEntity baseEntity = BuildManager.SpawnChildEntity(mainEntity, regularEntityData.prefabName, localPosition, localRotation, isDecor: false, enableSaving: true);
                    if (baseEntity is PercentFullStorageContainer)
                        baseEntity.SetFlag(BaseEntity.Flags.Busy, true);

                    HotAirBalloon hotAirBalloon = baseEntity as HotAirBalloon;
                    if (hotAirBalloon != null)
                    {
                        Rigidbody rigidbody = hotAirBalloon.myRigidbody;
                        rigidbody.isKinematic = true;
                        rigidbody.freezeRotation = true;
                        hotAirBalloon.inflationLevel = 1;
                        hotAirBalloon.enabled = false;
                        hotAirBalloon.SendNetworkUpdate();
                        hotAirBalloon.enableSaving = false;
                    }
                }

                foreach (BoxColliderData colliderData in siteData.preventBuildingColliders)
                    CreateBoxCollider(colliderData, offset, 29);

                if (siteData.wireDatas != null)
                {
                    foreach (WireData wireData in siteData.wireDatas)
                    {
                        Vector3 localStartPosition = wireData.startPosition.ToVector3() + offset;
                        Vector3 localEndPosition = wireData.endPosition.ToVector3() + offset;

                        Vector3 globalStartPosition = PositionDefiner.GetGlobalPosition(mainEntity.transform, localStartPosition);
                        Vector3 globalEndPosition = PositionDefiner.GetGlobalPosition(mainEntity.transform, localEndPosition);

                        CustomDoorManipulator doorManipulator = BuildManager.SpawnChildEntity(mainEntity, "assets/prefabs/deployable/playerioents/doormanipulators/doorcontroller.deployed.prefab", localStartPosition, Vector3.zero, isDecor: false, enableSaving: false) as CustomDoorManipulator;
                        IOEntity.IOSlot ioOutput = doorManipulator.outputs[0];
                        ioOutput.connectedTo.entityRef.uid = doorManipulator.net.ID;
                        ioOutput.connectedTo = new IOEntity.IORef();
                        ioOutput.connectedTo.Set(doorManipulator);
                        ioOutput.connectedToSlot = 0;

                        ioOutput.linePoints = new List<Vector3> { new Vector3(0, 0, 0), PositionDefiner.GetLocalPosition(doorManipulator.transform, globalEndPosition) }.ToArray();
                        ioOutput.connectedTo.Init();
                        doorManipulator.MarkDirtyForceUpdateOutputs();
                        doorManipulator.SendNetworkUpdate();
                    }
                }

                if (!mainEntity.children.Any(x => x != null && x.GetBuildingPrivilege() != null))
                    mapMarker = MapMarker.CreateMarker(mainEntity.transform.position);
            }

            void CreateBoxCollider(BoxColliderData colliderData, Vector3 offset, int layer)
            {
                Vector3 size = colliderData.size.ToVector3();
                Vector3 localPosition = colliderData.position.ToVector3() + offset;
                Vector3 localRotation = colliderData.rotation.ToVector3();

                GameObject gameObject = new GameObject(layer == 29 ? "PreventBuildingCollider" : "PreventMovementCollider");
                gameObject.transform.localPosition = localPosition;
                gameObject.transform.localEulerAngles = localRotation;
                gameObject.transform.SetParent(mainEntity.transform, false);
                gameObject.layer = layer;

                BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
                boxCollider.size = size;
                boxCollider.center = Vector3.zero;
                shoudDestroyAfterUnload.Add(gameObject);
            }

            bool IsSiteEntity(BaseEntity entity)
            {
                if (entity.net != null && entity.net.ID.Value == mainEntity.net.ID.Value)
                    return true;

                BaseEntity parentEntity = entity.GetParentEntity();

                if (parentEntity == null || parentEntity.net == null)
                    return false;

                if (parentEntity.net.ID.Value == mainEntity.net.ID.Value)
                    return true;

                return false;
            }

            internal void SetSiteOwner(ulong userID)
            {
                mainEntity.OwnerID = userID;
            }

            internal static void UnloadSites()
            {
                foreach (BuildingSite buildingSite in buildingSites)
                    if (buildingSite != null)
                        buildingSite.UnloadSite();
            }

            internal void UnloadSite()
            {
                DeleteMapMarker();

                foreach (GameObject gameObject in shoudDestroyAfterUnload)
                    if (gameObject != null)
                        UnityEngine.GameObject.Destroy(gameObject);
            }

            internal void DeleteMapMarker()
            {
                if (mapMarker != null)
                    mapMarker.Delete();
            }

            internal static void KillAllSites()
            {
                foreach (BuildingSite buildingSites in buildingSites)
                    if (buildingSites != null && buildingSites.mainEntity.IsExists())
                        buildingSites.KillSite();
            }

            internal void KillSite()
            {
                if (mainEntity.IsExists())
                    mainEntity.Kill();
            }

            void OnDestroy()
            {
                UnloadSite();
            }
        }

        class MapMarker : FacepunchBehaviour
        {
            MapMarkerGenericRadius radiusMarker;
            VendingMachineMapMarker vendingMarker;
            Coroutine updateCounter;

            internal static MapMarker CreateMarker(Vector3 position)
            {
                if (!ins._config.markerConfig.enable)
                    return null;

                GameObject gameObject = new GameObject();
                gameObject.transform.position = position;
                gameObject.layer = (int)Rust.Layer.Reserved1;
                MapMarker mapMarker = gameObject.AddComponent<MapMarker>();
                mapMarker.Init();
                return mapMarker;
            }

            void Init()
            {
                CreateRadiusMarker();
                CreateVendingMarker();
                updateCounter = ServerMgr.Instance.StartCoroutine(MarkerUpdateCounter());
            }

            void CreateRadiusMarker()
            {
                if (!ins._config.markerConfig.useRingMarker)
                    return;

                radiusMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", this.gameObject.transform.position) as MapMarkerGenericRadius;
                radiusMarker.enableSaving = false;
                radiusMarker.Spawn();
                radiusMarker.radius = ins._config.markerConfig.radius;
                radiusMarker.alpha = ins._config.markerConfig.alpha;
                radiusMarker.color1 = new Color(ins._config.markerConfig.color1.r, ins._config.markerConfig.color1.g, ins._config.markerConfig.color1.b);
                radiusMarker.color2 = new Color(ins._config.markerConfig.color2.r, ins._config.markerConfig.color2.g, ins._config.markerConfig.color2.b);
            }

            void CreateVendingMarker()
            {
                if (!ins._config.markerConfig.useShopMarker)
                    return;

                vendingMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", this.gameObject.transform.position) as VendingMachineMapMarker;
                vendingMarker.enableSaving = false;
                vendingMarker.Spawn();
                vendingMarker.markerShopName = $"{ins._config.markerConfig.displayedName}";
                vendingMarker.SetFlag(BaseEntity.Flags.Busy, false);
                vendingMarker.SendNetworkUpdate();
            }

            IEnumerator MarkerUpdateCounter()
            {
                while (true)
                {
                    UpdateVendingMarker();
                    UpdateRadiusMarker();
                    yield return CoroutineEx.waitForSeconds(1f);
                }
            }

            void UpdateRadiusMarker()
            {
                if (!radiusMarker.IsExists())
                    return;

                radiusMarker.SendUpdate();
                radiusMarker.SendNetworkUpdate();
            }

            void UpdateVendingMarker()
            {
                if (!vendingMarker.IsExists())
                    return;

                vendingMarker.SetFlag(BaseEntity.Flags.Busy, true);
                vendingMarker.SendNetworkUpdate();
            }

            internal void Delete()
            {
                if (radiusMarker.IsExists())
                    radiusMarker.Kill();

                if (vendingMarker.IsExists())
                    vendingMarker.Kill();

                if (updateCounter != null)
                    ServerMgr.Instance.StopCoroutine(updateCounter);

                Destroy(this.gameObject);
            }
        }

        class Ch47Killer : FacepunchBehaviour
        {
            static HashSet<Ch47Killer> chKillers = new HashSet<Ch47Killer>();
            CH47HelicopterAIController ch47;

            internal static void AttachClass(CH47HelicopterAIController ch47)
            {
                Ch47Killer ch47Killer = ch47.gameObject.AddComponent<Ch47Killer>();
                ch47Killer.Init(ch47);
                chKillers.Add(ch47Killer);
            }

            internal static void Unload()
            {
                foreach (Ch47Killer ch47Killer in chKillers)
                    if (ch47Killer != null)
                        UnityEngine.GameObject.Destroy(ch47Killer);
            }

            void Init(CH47HelicopterAIController ch47)
            {
                this.ch47 = ch47;
            }

            void OnCollisionEnter(Collision collision)
            {
                if (collision == null || collision.collider == null)
                    return;

                BaseEntity entity = collision.GetEntity();
                if (entity == null)
                    return;

                BuildingSite buildingSite = BuildingSite.GetSiteByEntity(entity);
                if (buildingSite == null)
                    return;

                ins.NextTick(() =>
                {
                    if (ch47.IsExists())
                    {
                        ch47.DismountAllPlayers();
                        ch47.Kill(BaseNetworkable.DestroyMode.Gib);
                    }
                });
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

            internal static BuildingBlock SpawnChildBuildingBlock(string prefabName, int grade, uint color, ulong skin, Vector3 position, Vector3 rotation, BaseEntity parentEntity)
            {
                BuildingBlock buildingBlock = CreateEntity(prefabName, parentEntity.transform.position, Quaternion.identity, 0, true) as BuildingBlock;
                SetParent(parentEntity, buildingBlock, position, rotation);
                buildingBlock.AttachToBuilding(BuildingManager.server.NewBuildingID());
                buildingBlock.grounded = true;
                buildingBlock.cachedStability = 1;
                buildingBlock.Spawn();
                BuildingManager.server.decayEntities.Remove(buildingBlock);

                BuildingGrade.Enum buildingGrade = (BuildingGrade.Enum)grade;
                buildingBlock.ChangeGradeAndSkin(buildingGrade, skin);

                if (color != 0)
                    buildingBlock.SetCustomColour(color);

                return buildingBlock;
            }

            internal static void HideWirePoints(IOEntity entity)
            {
                entity.inputs = System.Array.Empty<IOEntity.IOSlot>();

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

            internal static BaseEntity CreateDecorEntity(string prefabName, Vector3 position, Quaternion rotation, ulong skinId = 0, bool enableSaving = false)
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
                DestroyEntityConponent<HittableByTrains>(entity);
                DestroyEntityConponents<TriggerParent>(entity);
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
                        if (collider != null && !collider.isTrigger && collider.gameObject.layer != 29)
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

                if (entity is not HotAirBalloon)
                {
                    DestroyEntityConponent<Rigidbody>(entity);
                }
            }

            internal static void DestroyEntityConponent<TypeForDestroy>(BaseEntity entity) where TypeForDestroy : UnityEngine.Object
            {
                if (entity == null)
                    return;

                TypeForDestroy component = entity.GetComponent<TypeForDestroy>();
                if (component != null)
                    UnityEngine.GameObject.DestroyImmediate(component);
            }

            internal static void DestroyEntityConponents<TypeForDestroy>(BaseEntity entity) where TypeForDestroy : UnityEngine.Object
            {
                if (entity == null)
                    return;

                TypeForDestroy[] components = entity.GetComponentsInChildren<TypeForDestroy>();

                for (int i = 0; i < components.Length; i++)
                {
                    TypeForDestroy component = components[i];

                    if (component != null)
                        UnityEngine.GameObject.DestroyImmediate(component);
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

        static class LocationDefiner
        {
            internal static Vector3 GetGlobalPosition(Transform parentTransform, Vector3 position)
            {
                return parentTransform.transform.TransformPoint(position);
            }

            internal static Quaternion GetGlobalRotation(Transform parentTransform, Vector3 rotation)
            {
                return parentTransform.rotation * Quaternion.Euler(rotation);
            }
        }

        static class MapSaver
        {
            static Vector3 loactionCenterPosition = new Vector3(0, 150, 0);
            static float radiusForSaving = 70;
            static Dictionary<string, string> colliderToPrefabs = new Dictionary<string, string>
            {
                ["wall.frame.fence"] = "assets/prefabs/building/wall.frame.fence/wall.frame.fence.prefab",
                ["glass_collider"] = "assets/prefabs/building/wall.window.reinforcedglass/wall.window.glass.reinforced.prefab",
            };

            internal static void SaveMap(string dataFileName)
            {
                BuildingSiteData buildingSiteData = new BuildingSiteData();
                SaveAllEntitiesInRadius(ref buildingSiteData);
                ins.SaveDataFile<BuildingSiteData>(buildingSiteData, dataFileName);
            }

            static void SaveAllEntitiesInRadius(ref BuildingSiteData buildingSiteData)
            {
                buildingSiteData.offset = "(0, 0, 0)";

                buildingSiteData.decorEntities = new HashSet<EntityData>();
                buildingSiteData.regularEntities = new HashSet<EntityData>();
                buildingSiteData.buildingBlocks = new HashSet<BuildingBlockData>();
                buildingSiteData.preventBuildingColliders = new HashSet<BoxColliderData>();

                List<Collider> colliders = Physics.OverlapSphere(loactionCenterPosition, radiusForSaving).Where(x => true).OrderBy(x => x.transform.position.z);

                foreach (Collider collider in colliders)
                {
                    if (collider.name.Contains("prevent_building"))
                    {
                        BoxCollider boxCollider = collider.gameObject.GetComponentInChildren<BoxCollider>();
                        if (boxCollider == null)
                            continue;

                        BoxColliderData preventBuildingColliderData = new BoxColliderData();
                        preventBuildingColliderData.position = GetPosition(boxCollider.transform);
                        preventBuildingColliderData.rotation = GetRotation(boxCollider.transform);
                        preventBuildingColliderData.size = $"({boxCollider.transform.localScale.x}, {boxCollider.transform.localScale.y}, {boxCollider.transform.localScale.z})";

                        if (preventBuildingColliderData != null && !buildingSiteData.preventBuildingColliders.Any(x => x.position == preventBuildingColliderData.position && x.rotation == preventBuildingColliderData.rotation && x.size == preventBuildingColliderData.size))
                            buildingSiteData.preventBuildingColliders.Add(preventBuildingColliderData);

                        continue;
                    }
                    if (collider.name.Contains("building core"))
                    {
                        BuildingBlockData buildingBlockData = new BuildingBlockData();
                        buildingBlockData.prefabName = GetBuildingBlockPrefabName(collider.name);
                        buildingBlockData.grade = 3;
                        buildingBlockData.position = GetPosition(collider.transform);
                        buildingBlockData.rotation = GetRotation(collider.transform);

                        if (buildingBlockData != null && !buildingSiteData.buildingBlocks.Any(x => x.prefabName == buildingBlockData.prefabName && x.position == buildingBlockData.position && x.rotation == buildingBlockData.rotation))
                            buildingSiteData.buildingBlocks.Add(buildingBlockData);

                        continue;
                    }

                    string redfinedPrefab;
                    if (colliderToPrefabs.TryGetValue(collider.name, out redfinedPrefab))
                    {
                        EntityData entityData = new EntityData();
                        entityData.prefabName = redfinedPrefab;
                        entityData.position = GetPosition(collider.transform);
                        entityData.rotation = GetRotation(collider.transform);

                        if (!buildingSiteData.decorEntities.Any(x => x.prefabName == entityData.prefabName && x.position == entityData.position && x.rotation == entityData.rotation))
                            buildingSiteData.decorEntities.Add(entityData);
                        continue;
                    }

                    BaseEntity entity = collider.ToBaseEntity();
                    if (entity == null || entity is BasePlayer or BaseAnimalNPC or BaseCorpse or LootContainer or CCTV_RC or ReactiveTarget)
                        continue;

                    else
                    {
                        EntityData entityData = new EntityData();
                        entityData.prefabName = entity.PrefabName;
                        entityData.skin = entity.skinID;
                        entityData.position = GetPosition(entity.transform);
                        entityData.rotation = GetRotation(entity.transform);

                        if (entity is NPCDwelling or BaseOven or SimpleBuildingBlock or Barricade or CargoShipContainer || entity.ShortPrefabName == "door.hinged.shipping_container")
                        {
                            if (!buildingSiteData.decorEntities.Any(x => x.prefabName == entityData.prefabName && x.position == entityData.position && x.rotation == entityData.rotation))
                                buildingSiteData.decorEntities.Add(entityData);
                        }
                        else
                        {
                            if (!buildingSiteData.regularEntities.Any(x => x.prefabName == entityData.prefabName && x.position == entityData.position && x.rotation == entityData.rotation))
                                buildingSiteData.regularEntities.Add(entityData);
                        }
                    }

                    float distanceToCenter = Vector3.Distance(collider.transform.position, loactionCenterPosition);
                }
            }

            static string GetBuildingBlockPrefabName(string colliderName)
            {
                if (colliderName.Contains("foundation"))
                {
                    if (colliderName.Contains("triangle"))
                        return "assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab";
                    else if (colliderName.Contains("steps"))
                        return "assets/prefabs/building core/foundation.steps/foundation.steps.prefab";
                    else
                        return "assets/prefabs/building core/foundation/foundation.prefab";
                }
                else if (colliderName.Contains("ramp"))
                {
                    return "assets/prefabs/building core/ramp/ramp.prefab";
                }
                else if (colliderName.Contains("floor"))
                {
                    if (colliderName.Contains("frame"))
                    {
                        if (colliderName.Contains("triangle"))
                            return "assets/prefabs/building core/floor.triangle.frame/floor.triangle.frame.prefab";
                        else
                            return "assets/prefabs/building core/floor.frame/floor.frame.prefab";
                    }
                    else if (colliderName.Contains("triangle"))
                        return "assets/prefabs/building core/floor.triangle/floor.triangle.prefab";
                    else
                        return "assets/prefabs/building core/floor/floor.prefab";
                }
                else if (colliderName.Contains("wall"))
                {
                    if (colliderName.Contains("doorway"))
                        return "assets/prefabs/building core/wall.doorway/wall.doorway.prefab";
                    else if (colliderName.Contains("window"))
                        return "assets/prefabs/building core/wall.window/wall.window.prefab";
                    else if (colliderName.Contains("frame"))
                        return "assets/prefabs/building core/wall.frame/wall.frame.prefab";
                    else if (colliderName.Contains("half"))
                        return "assets/prefabs/building core/wall.half/wall.half.prefab";
                    else if (colliderName.Contains("low"))
                        return "assets/prefabs/building core/wall.low/wall.low.prefab";
                    else
                        return "assets/prefabs/building core/wall/wall.prefab";
                }
                else if (colliderName.Contains("stair"))
                {
                    if (colliderName.Contains("spiral"))
                    {
                        if (colliderName.Contains("triangle"))
                            return "assets/prefabs/building core/stairs.spiral.triangle/block.stair.spiral.triangle.prefab";
                        else
                            return "assets/prefabs/building core/stairs.spiral/block.stair.spiral.prefab";
                    }
                    else if (colliderName.Contains("ushape"))
                        return "assets/prefabs/building core/stairs.u/block.stair.ushape.prefab";
                    else if (colliderName.Contains("lshape"))
                        return "assets/prefabs/building core/stairs.l/block.stair.lshape.prefab";
                }
                else if (colliderName.Contains("roof"))
                {
                    if (colliderName.Contains("triangle"))
                        return "assets/prefabs/building core/roof.triangle/roof.triangle.prefab";
                    else
                        return "assets/prefabs/building core/roof/roof.prefab";
                }

                return null;
            }

            static int GetBuildingBlockGrade(string colliderName)
            {
                if (colliderName.Contains("wood"))
                    return 1;
                else if (colliderName.Contains("stone"))
                    return 2;
                else if (colliderName.Contains("metal"))
                    return 3;
                else if (colliderName.Contains("toptier"))
                    return 4;

                return 0;
            }

            static string GetPosition(Transform transform)
            {
                Vector3 localPosition = transform.position - loactionCenterPosition;
                return $"({localPosition.x}, {localPosition.y}, {localPosition.z})";
            }

            static string GetRotation(Transform transform)
            {
                return transform.eulerAngles.ToString();
            }
        }

        static class NotifyManagerLite
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
            }

            internal static void SendMessageToPlayer(BasePlayer player, string langKey, params object[] args)
            {
                object[] argsClone = new object[args.Length];

                for (int i = 0; i < args.Length; i++)
                    argsClone[i] = args[i];

                for (int i = 0; i < argsClone.Length; i++)
                    if (argsClone[i] is int)
                        argsClone[i] = GetTimeMessage(player.UserIDString, (int)argsClone[i]);

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
        }

        static class PositionDefiner
        {
            internal static Vector3 GetLocalPosition(Transform parentTransform, Vector3 globalPosition)
            {
                return parentTransform.InverseTransformPoint(globalPosition);
            }

            internal static Vector3 GetGlobalPosition(Transform parentTransform, Vector3 position)
            {
                return parentTransform.transform.TransformPoint(position);
            }

            internal static Quaternion GetGlobalRotation(Transform parentTransform, Vector3 rotation)
            {
                return parentTransform.rotation * Quaternion.Euler(rotation);
            }

            internal static Vector3 GetRandomMapPoint()
            {
                Vector2 randomVector2 = World.Size * 0.6f * UnityEngine.Random.insideUnitCircle;
                Vector3 randomVector3 = PositionDefiner.GetGroundPosition(new Vector3(randomVector2.x, 0, randomVector2.y));
                return randomVector3;
            }

            internal static Vector3 GetGroundPosition(Vector3 position)
            {
                position.y = 500;
                RaycastHit raycastHit;

                int layerMask = 1 << 16 | 1 << 23;

                if (Physics.Raycast(position, Vector3.down, out raycastHit, 500, layerMask))
                    position.y = raycastHit.point.y;
                else
                    position.y = 0;

                return position;
            }

            internal static BaseEntity RaycastAll<T>(Ray ray, float distance = 50) where T : BaseEntity
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

            internal static bool IsPositionOnCargoPath(Vector3 position)
            {
                foreach (CargoShip.HarborInfo harbotInfo in CargoShip.harbors)
                {
                    IAIPathNode pathNode = harbotInfo.harborPath.GetClosestToPoint(position);

                    if (Vector3.Distance(pathNode.Position, position) < 90)
                        return true;
                }

                float distanceToCargoPath = GetDistanceToCargoPath(position);
                return distanceToCargoPath < 100;
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

        static class PermissionManager
        {
            internal static void RegisterPermissions()
            {
                foreach (BaseSiteConfig baseSiteConfig in ins._config.groundTypeConfig.sites)
                    if (!string.IsNullOrEmpty(baseSiteConfig.permission))
                        ins.permission.RegisterPermission(baseSiteConfig.permission, ins);

                foreach (BaseSiteConfig baseSiteConfig in ins._config.waterTypeConfig.sites)
                    if (!string.IsNullOrEmpty(baseSiteConfig.permission))
                        ins.permission.RegisterPermission(baseSiteConfig.permission, ins);

                foreach (BaseSiteConfig baseSiteConfig in ins._config.skyTypeConfig.sites)
                    if (!string.IsNullOrEmpty(baseSiteConfig.permission))
                        ins.permission.RegisterPermission(baseSiteConfig.permission, ins);
            }

            internal static bool IsUserHavePermission(string userIdString, string permissionName)
            {
                return ins.permission.UserHasPermission(userIdString, permissionName);
            }
        }
        #endregion Classes

        #region Data
        Dictionary<string, BuildingSiteData> siteCustomizationDatas = new Dictionary<string, BuildingSiteData>();

        bool TryLoadData()
        {
            foreach (BaseSiteConfig siteConfig in _config.groundTypeConfig.sites)
            {
                if (!TryLoadSiteDataFile(siteConfig.dataFileName))
                    return false;
            }

            foreach (BaseSiteConfig siteConfig in _config.waterTypeConfig.sites)
            {
                if (!TryLoadSiteDataFile(siteConfig.dataFileName))
                    return false;
            }

            foreach (BaseSiteConfig siteConfig in _config.skyTypeConfig.sites)
            {
                if (!TryLoadSiteDataFile(siteConfig.dataFileName))
                    return false;
            }

            return true;
        }

        bool TryLoadSiteDataFile(string path)
        {
            BuildingSiteData siteData = LoadDataFile<BuildingSiteData>($"{path}");

            if (siteData == null || siteData.buildingBlocks == null)
                return false;

            if (!siteCustomizationDatas.ContainsKey(path))
                siteCustomizationDatas.Add(path, siteData);

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

        public class BuildingSiteData
        {
            [JsonProperty("Offset")] public string offset { get; set; }
            [JsonProperty("Building blocks")] public HashSet<BuildingBlockData> buildingBlocks { get; set; }
            [JsonProperty("Regular Entities")] public HashSet<EntityData> regularEntities { get; set; }
            [JsonProperty("Decor Entities")] public HashSet<EntityData> decorEntities { get; set; }
            [JsonProperty("Prevent Buildong Colliders")] public HashSet<BoxColliderData> preventBuildingColliders { get; set; }
            [JsonProperty("Prevent Movement Colliders")] public HashSet<BoxColliderData> preventMovementColliders { get; set; }
            [JsonProperty("Wire Datas")] public HashSet<WireData> wireDatas { get; set; }
        }

        public class BuildingBlockData : EntityData
        {
            [JsonProperty("Grade [0 - 4]", Order = 102)] public int grade { get; set; }
            [JsonProperty("Color", Order = 103)] public uint color { get; set; }
        }

        public class EntityData : LocationData
        {
            [JsonProperty("Prefab")] public string prefabName { get; set; }
            [JsonProperty("Skin")] public ulong skin { get; set; }
        }

        public class BoxColliderData : LocationData
        {
            [JsonProperty("Size")] public string size { get; set; }
        }

        public class LocationData
        {
            [JsonProperty("Position", Order = 100)] public string position { get; set; }
            [JsonProperty("Rotation", Order = 101)] public string rotation { get; set; }
        }

        public class WireData
        {
            [JsonProperty("Start Position")] public string startPosition { get; set; }
            [JsonProperty("End Position")] public string endPosition { get; set; }
        }
        #endregion Data

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ConfigNotFound_Exeption"] = "Конфигурация не найдена! ({0})",

                ["GroundFlare_Description"] = "{0} Найдите <color=#738d43>плоскую поверхность</color> и бросьте флаер на землю. Проще всего найти место на <color=#738d43>пляже</color>",
                ["WaterFlare_Description"] = "{0} Бросьте флаер в <color=#198be2>море</color> в отдалении от берега и маршрута карго",
                ["SkyFlare_Description"] = "{0} Бросьте флаер на землю и локация появится <color=#4990c4>над вами</color>",

                ["Position_NotSuitable"] = "Неподходящая позиция!",
                ["Position_Suitable"] = "Бросьте флаер в вашу позицию и убегите на 50 метров!",
                ["Spawn_Failed"] = "Неудалось заспавнить!",
                ["NoPermission"] = "У вас нет разрешения!",
                ["Unauthorized"] = "Вы не авторизованы в шкафу!",
                ["BlockedOnSite"] = "Этот предмет запрещен!",
                ["MaxSitesForPlayer"] = "Вы уже призвали максимальное число локаций!",

                ["Spawn_Start"] = "Начался спавн локации!",
                ["GotSite"] = "Вы получили место для строительства!",
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ConfigNotFound_Exeption"] = "Configuration not found! ({0})",
                ["DataFileNotFound_Exeption"] = "Data file not found! ({0})",
                ["PlayerNotFound_Exeption"] = "Player not found! ({0})",
                ["SpawnStart_Log"] = "The spawn of building sites has begun!",
                ["SpawnStop_Log"] = "The spawn has ended!",
                ["DataNotFound_Exeption"] = "Data files were not found, or are corrupted. Move the contents of the data folder from the archive to the oxide/data folder on your server!",

                ["GroundFlare_Description"] = "{0} Find a <color=#738d43>flat surface</color> and drop the flare to the ground. The easiest way is to find a place on the <color=#738d43>beach</color>",
                ["WaterFlare_Description"] = "{0} Throw the flare into the <color=#4990c4>sea</color> away from the shore and the cargo route",
                ["SkyFlare_Description"] = "{0} Throw the flare to the ground and the building site will spawn <color=#4990c4>above you</color>",

                ["Position_NotSuitable"] = "Wrong position!",
                ["Position_Suitable"] = "Throw the flyer to your position and run 50 meters away!",
                ["Spawn_Failed"] = "Failed to spawn!",
                ["NoPermission"] = "You don't have permission!",
                ["Unauthorized"] = "You are not authorized!",
                ["BlockedOnSite"] = "This item is prohibited!",
                ["MaxSitesForPlayer"] = "You have already summoned up the maximum number of locations!",

                ["Spawn_Start"] = "The spawn has begun!",
                ["GotSite"] = "You've got a Building Site!",
            }, this);
        }

        internal static string GetMessage(string langKey, string userID)
        {
            return ins.lang.GetMessage(langKey, ins, userID);
        }

        internal static string GetMessage(string langKey, string userID, params object[] args)
        {
            return (args.Length == 0) ? GetMessage(langKey, userID) : string.Format(GetMessage(langKey, userID), args);
        }
        #endregion Lang

        #region Configs
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
            [JsonProperty(en ? "The maximum number of locations that one player can summon (-1 - not limited)" : "Максимальное количество локаций, которое может вызвать один игрок (-1 - не ограничивать)", Order = 100)]
            public int maxLocationNumberPerPlayer { get; set; }

            [JsonProperty(en ? "Kill trees in the spawn position of locations? [true/false]" : "Удалять деревья в позиции спавна локаций? [true/false]", Order = 101)]
            public bool killTrees { get; set; }

            [JsonProperty(en ? "Will only authorized players be able to loot pumpjacks on the Building Site? [true/false]" : "Насосы на BuildingSite смогут лутать только авторизированные игроки? [true/false]", Order = 102)]
            public bool onlyAutedLootPumpjack { get; set; }
        }

        public class GroundTypeConfig : BaseTypeConfig
        {
            [JsonProperty(en ? "Allow spawn on rivers/lakes? [true/false]" : "Разрешить спавн на реках/озерах? [true/false]", Order = 100)]
            public bool isRiverSpawnEnabled { get; set; }

            [JsonProperty(en ? "Allow spawn on beaches? [true/false]" : "Разрешить спавн на пляжах? [true/false]", Order = 101)]
            public bool isBeachSpawnEnabled { get; set; }

            [JsonProperty(en ? "Maximum depth in the sea" : "Максимальная глубина в море", Order = 102)]
            public float maxDepht { get; set; }

            [JsonProperty(en ? "List of locations" : "Список локаций", Order = 103)]
            public HashSet<GroundSiteConfig> sites { get; set; }
        }

        public class WaterTypeConfig : BaseTypeConfig
        {
            [JsonProperty(en ? "Maximum distance to the shore" : "Максимальное расстояние до берега", Order = 100)]
            public int maxShoreDistance { get; set; }

            [JsonProperty(en ? "Minimum depth" : "Минимальная глубина", Order = 102)]
            public float minDepth { get; set; }

            [JsonProperty(en ? "List of locations" : "Список локаций", Order = 103)]
            public HashSet<WaterSiteConfig> sites { get; set; }
        }

        public class SkyTypeConfig : BaseTypeConfig
        {
            [JsonProperty(en ? "Minimum spawn altitude" : "Минмальная высота спавна", Order = 100)]
            public int minSpawnHeight { get; set; }

            [JsonProperty(en ? "Maximum spawn altitude" : "Максимальная высота спавна", Order = 101)]
            public int maxSpawnHeight { get; set; }

            [JsonProperty(en ? "Prohibit the deployment of turrets for locations of this type." : "Запретить устанавливать турели на локациях этого типа", Order = 103)]
            public bool isTurretsDisable { get; set; }

            [JsonProperty(en ? "Prohibit the deployment of SamSites for locations of this type." : "Запретить устанавливать ПВО на локациях этого типа", Order = 104)]
            public bool isSamsiteDisable { get; set; }

            [JsonProperty(en ? "List of locations" : "Список локаций", Order = 105)]
            public HashSet<SkySiteConfig> sites { get; set; }
        }

        public class BaseTypeConfig
        {
            [JsonProperty(en ? "Allow automatic spawn? [true/false]" : "Разрешить автоматический спавн? [true/false]")]
            public bool isAutoSpawn { get; set; }

            [JsonProperty(en ? "The minimum number of locations" : "Минимальное количество локаций этого типа", Order = 1)]
            public int minAmount { get; set; }

            [JsonProperty(en ? "The maximum number of locations" : "Максимальное количество локаций этого типа", Order = 2)]
            public int maxAmount { get; set; }
        }


        public class GroundSiteConfig : BaseSiteConfig
        {
            [JsonProperty(en ? "Maximum upward deviation in height" : "Максимальное отклонение по высоте вверх", Order = 100)]
            public float maxUpDeltaHeigh { get; set; }

            [JsonProperty(en ? "Maximum downward deviation in height" : "Максимальное отклонение по высоте вниз", Order = 101)]
            public float maxDownDeltaHeigh { get; set; }
        }

        public class WaterSiteConfig : BaseSiteConfig
        {
        }

        public class SkySiteConfig : BaseSiteConfig
        {
        }

        public class BaseSiteConfig
        {
            [JsonProperty(en ? "Preset Name" : "Название пресета")]
            public string presetName { get; set; }

            [JsonProperty(en ? "Data file Name" : "Название дата файла")]
            public string dataFileName { get; set; }

            [JsonProperty(en ? "Radius of the construction area" : "Радиус зоны для постройки")]
            public float insideRadius { get; set; }

            [JsonProperty(en ? "Location radius" : "Радиус локации")]
            public float outsideRadius { get; set; }

            [JsonProperty(en ? "Allow automatic spawn? [true/false]" : "Разрешить автоматический спавн? [true/false]")]
            public bool isAutoSpawn { get; set; }

            [JsonProperty(en ? "Probability " : "Вероятность спавна")]
            public float probability { get; set; }

            [JsonProperty(en ? "Permission to summon BuildingSite" : "Разрешение для призыва локации")]
            public string permission { get; set; }

            [JsonProperty(en ? "Item" : "Предмет для спавна")]
            public ItemConfig itemConfig { get; set; }
        }


        public class ItemConfig
        {
            [JsonProperty("ShortName")]
            public string shortname { get; set; }

            [JsonProperty("SkinID (0 - default)")]
            public ulong skin { get; set; }

            [JsonProperty(en ? "Name (empty - default)" : "Название (empty - default)")]
            public string name { get; set; }
        }

        public class MarkerConfig
        {
            [JsonProperty(en ? "Гse a marker for free locations? [true/false]" : "Использовать маркер для свободных локаций? [true/false]")] public bool enable { get; set; }
            [JsonProperty(en ? "Display Name" : "Отображаемое имя")] public string displayedName { get; set; }
            [JsonProperty(en ? "Use a vending marker? [true/false]" : "Добавить маркер магазина? [true/false]")] public bool useShopMarker { get; set; }
            [JsonProperty(en ? "Use a circular marker? [true/false]" : "Добавить круговой маркер? [true/false]")] public bool useRingMarker { get; set; }
            [JsonProperty(en ? "Radius" : "Радиус")] public float radius { get; set; }
            [JsonProperty(en ? "Alpha" : "Прозрачность")] public float alpha { get; set; }
            [JsonProperty(en ? "Marker color" : "Цвет маркера")] public ColorConfig color1 { get; set; }
            [JsonProperty(en ? "Outline color" : "Цвет контура")] public ColorConfig color2 { get; set; }
        }

        public class ColorConfig
        {
            [JsonProperty("r")] public float r { get; set; }
            [JsonProperty("g")] public float g { get; set; }
            [JsonProperty("b")] public float b { get; set; }
        }

        public class NotifyConfig : BaseMessageConfig
        {
            [JsonProperty(en ? "Redefined messages" : "Переопределенные сообщения )", Order = 101)]
            public HashSet<RedefinedMessageConfig> redefinedMessages { get; set; }
        }

        public class RedefinedMessageConfig : BaseMessageConfig
        {
            [JsonProperty(en ? "Enable this message? [true/false]" : "Включить сообщение? [true/false]", Order = 1)]
            public bool isEnable { get; set; }

            [JsonProperty("Lang Key", Order = 1)]
            public string langKey { get; set; }
        }

        public class BaseMessageConfig
        {
            [JsonProperty(en ? "Chat Message setting" : "Настройки сообщений в чате", Order = 1)]
            public ChatConfig chatConfig { get; set; }

            [JsonProperty(en ? "Facepunch Game Tips setting" : "Настройка сообщений Facepunch Game Tip", Order = 2)]
            public GameTipConfig gameTipConfig { get; set; }
        }

        public class ChatConfig
        {
            [JsonProperty(en ? "Use chat notifications? [true/false]" : "Использовать ли чат? [true/false]")]
            public bool isEnabled { get; set; }
        }

        public class GameTipConfig
        {
            [JsonProperty(en ? "Use Facepunch Game Tips (notification bar above hotbar)? [true/false]" : "Использовать ли Facepunch Game Tip (оповещения над слотами быстрого доступа игрока)? [true/false]")]
            public bool isEnabled { get; set; }

            [JsonProperty(en ? "Style (0 - Blue Normal, 1 - Red Normal, 2 - Blue Long, 3 - Blue Short, 4 - Server Event)" : "Стиль (0 - Blue Normal, 1 - Red Normal, 2 - Blue Long, 3 - Blue Short, 4 - Server Event)")]
            public int style { get; set; }
        }

        private class PluginConfig
        {
            [JsonProperty(en ? "Version" : "Версия")]
            public VersionNumber version { get; set; }

            [JsonProperty(en ? "Chat Prefix" : "Префикс в чате")]
            public string prefix { get; set; }

            [JsonProperty(en ? "General Setting" : "Основные настройки")]
            public MainConfig mainConfig { get; set; }

            [JsonProperty(en ? "Ground locations" : "Наземные локации")]
            public GroundTypeConfig groundTypeConfig { get; set; }

            [JsonProperty(en ? "Islands" : "Острова")]
            public WaterTypeConfig waterTypeConfig { get; set; }

            [JsonProperty(en ? "Sky locations" : "Воздушные локации")]
            public SkyTypeConfig skyTypeConfig { get; set; }

            [JsonProperty(en ? "Marker Setting" : "Настройки маркера")]
            public MarkerConfig markerConfig { get; set; }

            [JsonProperty(en ? "Notification Settings" : "Настройки уведомлений")]
            public NotifyConfig notifyConfig { get; set; }

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    version = new VersionNumber(1, 0, 7),
                    prefix = "[BuildingSites]",
                    mainConfig = new MainConfig
                    {
                        maxLocationNumberPerPlayer = -1
                    },
                    groundTypeConfig = new GroundTypeConfig
                    {
                        isAutoSpawn = false,
                        minAmount = 3,
                        maxAmount = 3,
                        isRiverSpawnEnabled = true,
                        isBeachSpawnEnabled = true,
                        maxDepht = 2f,
                        sites = new HashSet<GroundSiteConfig>
                        {
                            new GroundSiteConfig
                            {
                                presetName = "cave_1",
                                dataFileName = "cave_1",
                                insideRadius = 21,
                                outsideRadius = 40,
                                isAutoSpawn = true,
                                probability = 20f,
                                permission = "",
                                itemConfig = new ItemConfig
                                {
                                    shortname = "flare",
                                    name = "Building Site",
                                    skin = 3387696053,
                                },
                                maxUpDeltaHeigh = 4f,
                                maxDownDeltaHeigh = 4f
                            },
                            new GroundSiteConfig
                            {
                                presetName = "platform_1",
                                dataFileName = "platform_1",
                                insideRadius = 5,
                                outsideRadius = 41,
                                isAutoSpawn = true,
                                probability = 20f,
                                permission = "",
                                itemConfig = new ItemConfig
                                {
                                    shortname = "flare",
                                    name = "Building Site",
                                    skin = 3387684797,
                                },
                                maxUpDeltaHeigh = 5f,
                                maxDownDeltaHeigh = 9f
                            },
                            new GroundSiteConfig
                            {
                                presetName = "platform_2",
                                dataFileName = "platform_2",
                                insideRadius = 40,
                                outsideRadius = 45,
                                isAutoSpawn = true,
                                probability = 20f,
                                permission = "",
                                itemConfig = new ItemConfig
                                {
                                    shortname = "flare",
                                    name = "Building Site",
                                    skin = 3387684995,
                                },
                                maxUpDeltaHeigh = 16,
                                maxDownDeltaHeigh = 8
                            },
                            new GroundSiteConfig
                            {
                                presetName = "ruins_1",
                                dataFileName = "ruins_1",
                                insideRadius = 41,
                                outsideRadius = 55,
                                isAutoSpawn = true,
                                probability = 20f,
                                permission = "",
                                itemConfig = new ItemConfig
                                {
                                    shortname = "flare",
                                    name = "Building Site",
                                    skin = 3387685498,
                                },
                                maxUpDeltaHeigh = 5,
                                maxDownDeltaHeigh = 10
                            },
                            new GroundSiteConfig
                            {
                                presetName = "ruins_2",
                                dataFileName = "ruins_2",
                                insideRadius = 14,
                                outsideRadius = 30,
                                isAutoSpawn = true,
                                probability = 20f,
                                permission = "",
                                itemConfig = new ItemConfig
                                {
                                    shortname = "flare",
                                    name = "Building Site",
                                    skin = 3387686922,
                                },
                                maxUpDeltaHeigh = 5,
                                maxDownDeltaHeigh = 10
                            },
                            new GroundSiteConfig
                            {
                                presetName = "ruins_3",
                                dataFileName = "ruins_3",
                                insideRadius = 26,
                                outsideRadius = 35,
                                isAutoSpawn = true,
                                probability = 20f,
                                permission = "",
                                itemConfig = new ItemConfig
                                {
                                    shortname = "flare",
                                    name = "Building Site",
                                    skin = 3387686321,
                                },
                                maxUpDeltaHeigh = 3.5f,
                                maxDownDeltaHeigh = 10f
                            },
                            new GroundSiteConfig
                            {
                                presetName = "ruins_4",
                                dataFileName = "ruins_4",
                                insideRadius = 18,
                                outsideRadius = 35,
                                isAutoSpawn = true,
                                probability = 20f,
                                permission = "",
                                itemConfig = new ItemConfig
                                {
                                    shortname = "flare",
                                    name = "Building Site",
                                    skin = 3387685895,
                                },
                                maxUpDeltaHeigh = 8f,
                                maxDownDeltaHeigh = 8f
                            },
                            new GroundSiteConfig
                            {
                                presetName = "ruins_5",
                                dataFileName = "ruins_5",
                                insideRadius = 9f,
                                outsideRadius = 18f,
                                isAutoSpawn = true,
                                probability = 20f,
                                permission = "",
                                itemConfig = new ItemConfig
                                {
                                    shortname = "flare",
                                    name = "Building Site",
                                    skin = 3387687136,
                                },
                                maxUpDeltaHeigh = 5f,
                                maxDownDeltaHeigh = 10f
                            },
                            new GroundSiteConfig
                            {
                                presetName = "bunker_1",
                                dataFileName = "bunker_1",
                                insideRadius = 12,
                                outsideRadius = 35,
                                isAutoSpawn = true,
                                probability = 20f,
                                permission = "",
                                itemConfig = new ItemConfig
                                {
                                    shortname = "flare",
                                    name = "Building Site",
                                    skin = 3387687426,
                                },
                                maxUpDeltaHeigh = 4f,
                                maxDownDeltaHeigh = 4f
                            },
                            new GroundSiteConfig
                            {
                                presetName = "bunker_2",
                                dataFileName = "bunker_2",
                                insideRadius = 17f,
                                outsideRadius = 36.7f,
                                isAutoSpawn = true,
                                probability = 20f,
                                permission = "",
                                itemConfig = new ItemConfig
                                {
                                    shortname = "flare",
                                    name = "Building Site",
                                    skin = 3387687572,
                                },
                                maxUpDeltaHeigh = 5f,
                                maxDownDeltaHeigh = 5f
                            }
                        }
                    },
                    waterTypeConfig = new WaterTypeConfig
                    {
                        isAutoSpawn = false,
                        minAmount = 2,
                        maxAmount = 2,
                        maxShoreDistance = 350,
                        minDepth = 4f,
                        sites = new HashSet<WaterSiteConfig>
                        {
                            new WaterSiteConfig
                            {
                                presetName = "island_1",
                                dataFileName = "island_1",
                                insideRadius = 42.2f,
                                outsideRadius = 50f,
                                isAutoSpawn = true,
                                probability = 20f,
                                permission = "",
                                itemConfig = new ItemConfig
                                {
                                    shortname = "flare",
                                    name = "Water Building Site",
                                    skin = 3387687815,
                                }
                            },
                            new WaterSiteConfig
                            {
                                presetName = "island_2",
                                dataFileName = "island_2",
                                insideRadius = 40f,
                                outsideRadius = 50f,
                                isAutoSpawn = true,
                                probability = 20f,
                                permission = "",
                                itemConfig = new ItemConfig
                                {
                                    shortname = "flare",
                                    name = "Water Building Site",
                                    skin = 3387688043,
                                }
                            },
                            new WaterSiteConfig
                            {
                                presetName = "island_3",
                                dataFileName = "island_3",
                                insideRadius = 63,
                                outsideRadius = 75f,
                                isAutoSpawn = true,
                                probability = 20f,
                                permission = "",
                                itemConfig = new ItemConfig
                                {
                                    shortname = "flare",
                                    name = "Water Building Site",
                                    skin = 3387688286,
                                }
                            },
                            new WaterSiteConfig
                            {
                                presetName = "island_4",
                                dataFileName = "island_4",
                                insideRadius = 43,
                                outsideRadius = 50f,
                                isAutoSpawn = true,
                                probability = 20f,
                                permission = "",
                                itemConfig = new ItemConfig
                                {
                                    shortname = "flare",
                                    name = "Water Building Site",
                                    skin = 3387688429,
                                }
                            },
                        }
                    },
                    skyTypeConfig = new SkyTypeConfig
                    {
                        isAutoSpawn = false,
                        minAmount = 1,
                        maxAmount = 1,
                        minSpawnHeight = 100,
                        maxSpawnHeight = 160,
                        isSamsiteDisable = false,
                        isTurretsDisable = false,
                        sites = new HashSet<SkySiteConfig>
                        {
                            new SkySiteConfig
                            {
                                presetName = "sky_1",
                                dataFileName = "sky_1",
                                insideRadius = 43f,
                                outsideRadius = 50,
                                isAutoSpawn = true,
                                probability = 20f,
                                permission = "",
                                itemConfig = new ItemConfig
                                {
                                    shortname = "flare",
                                    name = "Flying Building Site",
                                    skin = 3387688822,
                                }
                            },
                            new SkySiteConfig
                            {
                                presetName = "sky_2",
                                dataFileName = "sky_2",
                                insideRadius = 32,
                                outsideRadius = 40,
                                isAutoSpawn = true,
                                probability = 20f,
                                permission = "",
                                itemConfig = new ItemConfig
                                {
                                    shortname = "flare",
                                    name = "Flying Building Site",
                                    skin = 3387689017,
                                }
                            },
                            new SkySiteConfig
                            {
                                presetName = "sky_3",
                                dataFileName = "sky_3",
                                insideRadius = 26,
                                outsideRadius = 35,
                                isAutoSpawn = true,
                                probability = 20f,
                                permission = "",
                                itemConfig = new ItemConfig
                                {
                                    shortname = "flare",
                                    name = "Flying Building Site",
                                    skin = 3387689198,
                                }
                            }
                        }
                    },
                    markerConfig = new MarkerConfig
                    {
                        enable = true,
                        displayedName = en ? "Free building site" : "Свободное место под застройку",
                        useRingMarker = true,
                        useShopMarker = true,
                        radius = 0.2f,
                        alpha = 0.6f,
                        color1 = new ColorConfig { r = 0.2f, g = 0.8f, b = 0.1f },
                        color2 = new ColorConfig { r = 0f, g = 0f, b = 0f }
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
                        redefinedMessages = new HashSet<RedefinedMessageConfig>
                        {
                            new RedefinedMessageConfig
                            {
                                isEnable = true,
                                langKey = "Position_Suitable",
                                chatConfig = new ChatConfig
                                {
                                    isEnabled = false,
                                },
                                gameTipConfig = new GameTipConfig
                                {
                                    isEnabled = true,
                                    style = 2,
                                }
                            },
                            new RedefinedMessageConfig
                            {
                                isEnable = true,
                                langKey = "GotSite",
                                chatConfig = new ChatConfig
                                {
                                    isEnabled = false,
                                },
                                gameTipConfig = new GameTipConfig
                                {
                                    isEnabled = true,
                                    style = 2,
                                }
                            },
                            new RedefinedMessageConfig
                            {
                                isEnable = true,
                                langKey = "GroundFlare_Description",
                                chatConfig = new ChatConfig
                                {
                                    isEnabled = true,
                                },
                                gameTipConfig = new GameTipConfig
                                {
                                    isEnabled = false,
                                    style = 2,
                                }
                            },
                            new RedefinedMessageConfig
                            {
                                isEnable = true,
                                langKey = "WaterFlare_Description",
                                chatConfig = new ChatConfig
                                {
                                    isEnabled = true,
                                },
                                gameTipConfig = new GameTipConfig
                                {
                                    isEnabled = false,
                                    style = 2,
                                }
                            },
                            new RedefinedMessageConfig
                            {
                                isEnable = true,
                                langKey = "SkyFlare_Description",
                                chatConfig = new ChatConfig
                                {
                                    isEnabled = true,
                                },
                                gameTipConfig = new GameTipConfig
                                {
                                    isEnabled = false,
                                    style = 2,
                                }
                            }
                        }
                    },
                };
            }
        }
        #endregion Config
    }
}

namespace Oxide.Plugins.BuildingSitesExtensionMethods
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