using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using UnityEngine;
using Facepunch;
using Rust;

namespace Oxide.Plugins
{
    [Info("Stack Everything", "Cascade", "1.2.5")]
    [Description("Stack all entities and farm stacking with role-based limits and skin blacklists")] 
    public class StackEverything : RustPlugin
    {
        private const string PermUse = "stackeverything.use";
        private const string LangPrefix = "StackEverything";

        private readonly HashSet<ulong> spawnedByPlugin = new HashSet<ulong>();
        private readonly HashSet<ulong> ownersStacking = new HashSet<ulong>();
        private ConfigData config;
        private bool debug;

        private class VersionNumberData
        {
            public int Major;
            public int Minor;
            public int Patch;
        }

        private void OnEntityKill(BaseNetworkable net)
        {
            var entity = net as BaseEntity;
            if (entity?.net == null) return;
            spawnedByPlugin.Remove(entity.net.ID.Value);
        }

        private class RoleConfig
        {
            public int Priority;
            public int MaximumNumberOfStackableEntities;
            public List<string> ExcludeStackingOfTheseEntities = new List<string>();
            public Dictionary<string, int> MaximumStackNumberPerEntity = new Dictionary<string, int>();
        }

        private class StackItem
        {
            public string DisplayName;
            public string ItemName;
            public int ItemId;
            public bool EnableStacking;
            public string PrefabName;
            public float RadiusCheck;
            public float ColliderHeight;
            public float YOffset;
            public string EffectName;
        }

        private class ConfigData
        {
            public bool UseClanTeam;
            public bool ShareGroupWithClanTeamMembers;
            public bool EnableDynamicAutoStacking = true;
            public List<ulong> SpawnSkinIdBlacklist = new List<ulong>();
            public List<ulong> StackingSkinIdBlacklist = new List<ulong>();
            public Dictionary<string, RoleConfig> RolePermission = new Dictionary<string, RoleConfig>();
            public List<StackItem> StackableItems = new List<StackItem>();
            public VersionNumberData VersionNumber = new VersionNumberData { Major = 1, Minor = 2, Patch = 5 };
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData
            {
                UseClanTeam = true,
                ShareGroupWithClanTeamMembers = false,
                EnableDynamicAutoStacking = true,
                RolePermission = new Dictionary<string, RoleConfig>
                {
                    ["default"] = new RoleConfig
                    {
                        Priority = 100,
                        MaximumNumberOfStackableEntities = 2
                    },
                    ["vip1"] = new RoleConfig
                    {
                        Priority = 1,
                        MaximumNumberOfStackableEntities = 4
                    },
                    ["vip2"] = new RoleConfig
                    {
                        Priority = 2,
                        MaximumNumberOfStackableEntities = 6
                    },
                    ["vip3"] = new RoleConfig
                    {
                        Priority = 3,
                        MaximumNumberOfStackableEntities = 10
                    }
                },
                StackableItems = new List<StackItem>
                {
                    new StackItem{ DisplayName = "Large Water Catcher", ItemName = "water.catcher.large", ItemId = -1100168350, EnableStacking = true, PrefabName = "assets/prefabs/deployable/water catcher/water_catcher_large.prefab", RadiusCheck = 0.1f, ColliderHeight = 4.168319f, YOffset = 4.1f, EffectName = "assets/prefabs/deployable/water catcher/effects/water-catcher-large-deploy.prefab" },
                    new StackItem{ DisplayName = "Small Water Catcher", ItemName = "water.catcher.small", ItemId = -132247350, EnableStacking = true, PrefabName = "assets/prefabs/deployable/water catcher/water_catcher_small.prefab", RadiusCheck = 0.1f, ColliderHeight = 2.67044f, YOffset = 2.5f, EffectName = "assets/prefabs/deployable/water catcher/effects/water-catcher-deploy.prefab" },
                    new StackItem{ DisplayName = "Large Wood Box", ItemName = "box.wooden.large", ItemId = 833533164, EnableStacking = true, PrefabName = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab", RadiusCheck = 0.1f, ColliderHeight = 0.790511f, YOffset = 0.76f, EffectName = "assets/prefabs/deployable/large wood storage/effects/large-wood-box-deploy.prefab" },
                    new StackItem{ DisplayName = "Wood Storage Box", ItemName = "box.wooden", ItemId = -180129657, EnableStacking = true, PrefabName = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab", RadiusCheck = 0.1f, ColliderHeight = 0.5552952f, YOffset = 0.5552952f, EffectName = "assets/prefabs/deployable/woodenbox/effects/wooden-box-deploy.prefab" },
                    new StackItem{ DisplayName = "Furnace", ItemName = "furnace", ItemId = -1999722522, EnableStacking = true, PrefabName = "assets/prefabs/deployable/furnace/furnace.prefab", RadiusCheck = 0.1f, ColliderHeight = 1.6f, YOffset = 1.44f, EffectName = "assets/prefabs/deployable/furnace/effects/furnace-deploy.prefab" },
                    new StackItem{ DisplayName = "Electric Furnace", ItemName = "electric.furnace", ItemId = -1196547867, EnableStacking = true, PrefabName = "assets/prefabs/deployable/playerioents/electricfurnace/electricfurnace.deployed.prefab", RadiusCheck = 0.1f, ColliderHeight = 1.2f, YOffset = 1.15f, EffectName = "assets/prefabs/deployable/playerioents/electricfurnace/effects/electric-furnace-deploy.prefab" },
                    new StackItem{ DisplayName = "Composter", ItemName = "composter", ItemId = -1488398114, EnableStacking = true, PrefabName = "assets/prefabs/deployable/composter/composter.prefab", RadiusCheck = 0.1f, ColliderHeight = 1.64f, YOffset = 1.54f, EffectName = "assets/prefabs/deployable/furnace/effects/furnace-deploy.prefab" },
                    new StackItem{ DisplayName = "Locker", ItemName = "locker", ItemId = -110921842, EnableStacking = true, PrefabName = "assets/prefabs/deployable/locker/locker.deployed.prefab", RadiusCheck = 0.1f, ColliderHeight = 2.3f, YOffset = 2.238f, EffectName = "assets/prefabs/deployable/locker/effects/locker-deploy.prefab" }
                }
            };
            SaveConfig();
        }

        private void Init()
        {
            LoadConfigValues();
            permission.RegisterPermission(PermUse, this);
            foreach (var role in config.RolePermission.Keys)
                permission.RegisterPermission($"stackeverything.{role}", this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [$"{LangPrefix}.NoPermission"] = "Нет прав",
                [$"{LangPrefix}.Disabled"] = "Стакинг отключен",
                [$"{LangPrefix}.MaxReached"] = "Достигнут предел стакинга"
            }, this, "ru");
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [$"{LangPrefix}.NoPermission"] = "No permission",
                [$"{LangPrefix}.Disabled"] = "Stacking disabled",
                [$"{LangPrefix}.MaxReached"] = "Max stacking reached"
            }, this, "en");
        }

        private void LoadConfigValues()
        {
            try
            {
                config = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }
            if (config.VersionNumber == null) config.VersionNumber = new VersionNumberData { Major = 1, Minor = 2, Patch = 5 };
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config, true);
        }

        private void OnEntityBuilt(Planner planner, GameObject go)
        {
            if (planner == null || go == null) return;
            var player = planner.GetOwnerPlayer();
            if (player == null) return;
            var entity = go.GetComponent<BaseEntity>();
            if (entity == null) return;
            if (debug) SendReply(player, $"StackEverything: OnEntityBuilt {entity.PrefabName}");
            TryStack(player, entity);
        }

        private void OnEntitySpawned(BaseNetworkable net)
        {
            var entity = net as BaseEntity;
            if (entity == null) return;
            if (entity.net != null && spawnedByPlugin.Contains(entity.net.ID.Value)) return;
            var ownerId = entity.OwnerID;
            if (ownerId == 0) return;
            if (ownersStacking.Contains(ownerId)) return;
            var player = BasePlayer.FindByID(ownerId) ?? BasePlayer.FindSleeping(ownerId);
            if (player == null) return;
            if (debug) SendReply(player, $"StackEverything: OnEntitySpawned {entity.PrefabName}");
            TryStack(player, entity);
        }

        private void TryStack(BasePlayer player, BaseEntity baseEntity)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermUse)) return;
            if (!ownersStacking.Add(player.userID)) return;
            try
            {
            var prefab = baseEntity.PrefabName;
            var itemCfg = config.StackableItems.FirstOrDefault(x => x.EnableStacking && string.Equals(x.PrefabName, prefab, StringComparison.OrdinalIgnoreCase));
            if (itemCfg == null)
            {
                if (!config.EnableDynamicAutoStacking) return;
                var h = GetColliderHeight(baseEntity);
                itemCfg = new StackItem
                {
                    DisplayName = baseEntity.ShortPrefabName,
                    ItemName = string.Empty,
                    ItemId = 0,
                    EnableStacking = true,
                    PrefabName = prefab,
                    RadiusCheck = 0.05f,
                    ColliderHeight = h,
                    YOffset = h,
                    EffectName = string.Empty
                };
                if (debug) SendReply(player, $"StackEverything: Dynamic config for {prefab}, height {h:F2}");
            }
            if (config.StackingSkinIdBlacklist != null && config.StackingSkinIdBlacklist.Contains(baseEntity.skinID)) return;
            var role = ResolvePlayerRole(player);
            if (role == null) return;
            if (role.ExcludeStackingOfTheseEntities != null && role.ExcludeStackingOfTheseEntities.Any(x => string.Equals(x, prefab, StringComparison.OrdinalIgnoreCase))) return;
            var maxByRole = role.MaximumNumberOfStackableEntities;
            var maxByEntity = 0;
            if (role.MaximumStackNumberPerEntity != null)
            {
                if (role.MaximumStackNumberPerEntity.TryGetValue(prefab, out var m)) maxByEntity = m;
            }
            var max = Math.Max(0, Math.Max(maxByEntity, maxByRole));
            if (max > 5) max = 5;
            if (max <= 1) return;
            var teamCheckOwner = baseEntity.OwnerID;
            if (config.UseClanTeam && !CanStackOnOwnership(player, baseEntity)) return;
            var basePos = baseEntity.transform.position;
            var baseRot = baseEntity.transform.rotation;
            var skin = baseEntity.skinID;
            if (config.SpawnSkinIdBlacklist != null && config.SpawnSkinIdBlacklist.Contains(skin)) skin = 0;
            if (debug) SendReply(player, $"StackEverything: Stacking {prefab} up to {max} with skin {skin}");
            for (var i = 1; i < max; i++)
            {
                var stackHeight = itemCfg.YOffset > 0f ? itemCfg.YOffset : (itemCfg.ColliderHeight > 0f ? itemCfg.ColliderHeight : GetColliderHeight(baseEntity));
                if (stackHeight <= 0f) stackHeight = 1f;
                var spawnPos = basePos + new Vector3(0f, stackHeight * i, 0f);
                if (!IsPositionValid(spawnPos, itemCfg.RadiusCheck <= 0f ? 0.05f : itemCfg.RadiusCheck, baseEntity)) break;
                var ent = GameManager.server.CreateEntity(itemCfg.PrefabName, spawnPos, baseRot);
                if (ent == null) break;
                ent.skinID = skin;
                ent.OwnerID = player.userID;
                ent.Spawn();
                if (ent.net != null) spawnedByPlugin.Add(ent.net.ID.Value);
                if (!string.IsNullOrEmpty(itemCfg.EffectName)) Effect.server.Run(itemCfg.EffectName, spawnPos);
                if (debug) SendReply(player, $"StackEverything: Spawned at {spawnPos}");
            }
            }
            finally
            {
                ownersStacking.Remove(player.userID);
            }
        }

        private float GetColliderHeight(BaseEntity entity)
        {
            float height = 0f;
            var colliders = entity.GetComponentsInChildren<Collider>();
            if (colliders != null && colliders.Length > 0)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    var c = colliders[i];
                    if (c == null) continue;
                    var sizeY = c.bounds.size.y;
                    if (sizeY > height) height = sizeY;
                }
            }
            if (height <= 0f) height = 1f;
            return height;
        }

        private bool IsPositionValid(Vector3 pos, float radius, BaseEntity ignore)
        {
            var hits = Pool.GetList<Collider>();
            try
            {
                Vis.Colliders(pos, radius, hits, Layers.Mask.Prevent_Building | Layers.Mask.Trigger);
                if (hits.Count == 0) return true;
                for (int i = 0; i < hits.Count; i++)
                {
                    var h = hits[i];
                    if (h == null) continue;
                    if (ignore != null && h.transform != null && (h.transform == ignore.transform || h.transform.root == ignore.transform.root)) continue;
                    return false;
                }
                return true;
            }
            finally
            {
                hits.Clear();
                Pool.FreeList(ref hits);
            }
        }

        private RoleConfig ResolvePlayerRole(BasePlayer player)
        {
            var roles = new List<(string name, RoleConfig cfg)>();
            foreach (var kvp in config.RolePermission)
            {
                if (permission.UserHasPermission(player.UserIDString, $"stackeverything.{kvp.Key}")) roles.Add((kvp.Key, kvp.Value));
            }
            if (config.UseClanTeam && config.ShareGroupWithClanTeamMembers)
            {
                RelationshipManager.PlayerTeam team = null;
                var teams = RelationshipManager.ServerInstance?.teams;
                if (teams != null && teams.TryGetValue(player.currentTeam, out team) && team != null)
                {
                    foreach (var member in team.members)
                    {
                        var p = BasePlayer.FindByID(member) ?? BasePlayer.FindSleeping(member);
                        if (p == null) continue;
                        foreach (var kvp in config.RolePermission)
                        {
                            if (permission.UserHasPermission(p.UserIDString, $"stackeverything.{kvp.Key}") && roles.All(r => r.name != kvp.Key)) roles.Add((kvp.Key, kvp.Value));
                        }
                    }
                }
            }
            if (roles.Count == 0 && config.RolePermission.TryGetValue("default", out var def)) return def;
            var selected = roles.OrderBy(r => r.cfg.Priority).FirstOrDefault();
            return selected.cfg;
        }

        [ConsoleCommand("stackeverything.debug")]
        private void CmdDebug(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.Connection == null) return;
            var player = arg.Player();
            if (player == null || !player.IsAdmin) return;
            if (arg.Args != null && arg.Args.Length > 0)
            {
                debug = arg.GetInt(0) != 0;
            }
            SendReply(player, $"StackEverything debug: {(debug ? "ON" : "OFF")}");
        }

        private bool CanStackOnOwnership(BasePlayer player, BaseEntity target)
        {
            if (target == null) return true;
            if (target.OwnerID == 0) return true;
            if (target.OwnerID == player.userID) return true;
            if (!config.UseClanTeam) return false;
            RelationshipManager.PlayerTeam team = null;
            var teams = RelationshipManager.ServerInstance?.teams;
            if (teams == null || !teams.TryGetValue(player.currentTeam, out team) || team == null) return false;
            return team.members != null && team.members.Contains(target.OwnerID);
        }
    }
}
