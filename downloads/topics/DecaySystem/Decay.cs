using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Rust;
using UnityEngine;
using VLB;

namespace Oxide.Plugins
{
    [Info("Decay", "BlackPlugin.ru", "1.0.3")]
    public class Decay : RustPlugin
    {
        private static Decay Instance;
        Dictionary<string, ItemDefinition> deployable_list = new Dictionary<string, ItemDefinition>();
        Dictionary<string, Dictionary<string, object>> configDecay = new Dictionary<string, Dictionary<string, object>>();
        static Dictionary<string, object> configDecayDeployable = new Dictionary<string, object>();
        static Dictionary<string, object> configDecayBuild = new Dictionary<string, object>();
        private static int ticks;

        #region ConfigFunction

        string ListToString<T>(List<T> list, int first = 0, string seperator = ", ") => string.Join(seperator,
            (from val in list select val.ToString()).Skip(first).ToArray());

        void SetConfig(params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList();
            stringArgs.RemoveAt(args.Length - 1);
            if (Config.Get(stringArgs.ToArray()) == null) Config.Set(args);
        }

        T GetConfig<T>(T defaultVal, params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList();
            if (Config.Get(stringArgs.ToArray()) == null)
            {
                PrintError(
                    $"The plugin failed to read something from the config: {ListToString(stringArgs, 0, "/")}{Environment.NewLine}Please reload the plugin and see if this message is still showing. If so, please post this into the support thread of this plugin.");
                return defaultVal;
            }

            return (T)Convert.ChangeType(Config.Get(stringArgs.ToArray()), typeof(T));
        }

        #endregion

        IEnumerable<T> GetEntities<T>(Vector3 position, int radius)
            where T : BaseEntity
        {
            List<T> list = Pool.GetList<T>();
            Vis.Entities(position, radius, list, LayerMask.GetMask("Deployed", "Construction"));

            return list.Where(x => x.OwnerID != 0L);
        }

        protected override void LoadDefaultConfig()
        {
            build_dep_list();
            foreach (var deploy in deployable_list)
            {
                if(!configDecay.ContainsKey("Deployable"))
                    configDecay.Add("Deployable", new Dictionary<string, object>());
                if (configDecay["Deployable"].ContainsKey(deploy.Key)) continue;
                if (deploy.Value == null)
                {
                    configDecay["Deployable"].Add(deploy.Key, new Dictionary<string, object>
                    {
                        {"Decay In TC Range Time", 120 },
                        {"Decay Out TC Range Time", 120 },
                        {"Start Decay After", 120 },
                        {"ItemList", new List<string>{"Wood"}}
                    });
                    continue;
                }
                var item = ItemManager.CreateByItemID(deploy.Value.itemid);
                if (item != null)
                {
                    var prefab = item.CreateWorldObject(new Vector3(0, 0, 0));
                    if (prefab != null)
                    {
                        var prefabitem = GameManager.server.CreateEntity(prefab.PrefabName, Vector3.zero);
                        prefabitem.Spawn();
                        var basecombat = prefabitem.GetComponent<BaseCombatEntity>();
                        if (configDecay["Deployable"].ContainsKey(deploy.Key)) continue;
                        configDecay["Deployable"].Add(deploy.Key, new Dictionary<string, object>
                        {
                            {
                                "Decay In TC Range Time",
                                Math.Round(
                                    (basecombat == null ? 10800 : basecombat._health / (basecombat == null ? 10800 : basecombat._health / 1441 / 300) / 60 / 60 / 24),
                                    0)
                            },
                            {
                                "Decay Out TC Range Time", Math.Round(basecombat == null ? 10800 : basecombat._health / (basecombat == null ? 10800 : basecombat._health / 1441 / 300) / 60 / 60 / 24,0) / 2
                            },
                            {"Start Decay After", 120},
                            {"ItemList", new List<string> {"Wood"}}
                        });
                        prefabitem.Kill();
                        prefab.Kill();
                    }
                }
            }

            if (!configDecay.ContainsKey("Building"))
                configDecay.Add("Building", new Dictionary<string, object>());

            if(!configDecay["Building"].ContainsKey("Twigs"))
            configDecay["Building"].Add("Twigs", new Dictionary<string, object>{
                { "In TC Range", 3600},
                {"Out TC Range", 120 }
            });
            if (!configDecay["Building"].ContainsKey("Wood"))
            configDecay["Building"].Add("Wood", new Dictionary<string, object>{
                { "In TC Range", 10800},
                {"Out TC Range", 120 }
            });
            if (!configDecay["Building"].ContainsKey("Stone"))
            configDecay["Building"].Add("Stone", new Dictionary<string, object>{
                { "In TC Range", 18000},
                {"Out TC Range", 120 }
            });
            if (!configDecay["Building"].ContainsKey("Metal"))
            configDecay["Building"].Add("Metal", new Dictionary<string, object>{
                { "In TC Range", 28800},
                {"Out TC Range", 120 }
            });
            if (!configDecay["Building"].ContainsKey("TopTier"))
            configDecay["Building"].Add("TopTier", new Dictionary<string, object>{
                { "In TC Range", 43200},
                {"Out TC Range", 120 }
            });

            SetConfig("Building", configDecay["Building"]);
            SetConfig("Deployable", configDecay["Deployable"]);
            SaveConfig();
        }

        void Loaded()
        {
            Instance = this;
            configDecayDeployable = GetConfig(new Dictionary<string, object>(), "Deployable");
            configDecayBuild = GetConfig(new Dictionary<string, object>(), "Building");

            var entities = GetEntities<BaseEntity>(new Vector3(0, 0, 0), Convert.ToInt32(ConVar.Server.worldsize * 0.8f));
            var baseEntities = entities.ToList();
            foreach (var baseEntity in baseEntities)
            {
                if (baseEntity is BuildingPrivlidge) continue;
                var baseCb = baseEntity.GetComponent<BaseCombatEntity>();
                if (baseCb != null)
                    baseCb.GetOrAddComponent<DeployDecay>();
            }
            Pool.Free(ref entities);
        }

        void Unload()
        {
            DeleteAll<DeployDecay>();
        }

        class DeployDecay : MonoBehaviour
        {
            public BaseCombatEntity entity;
            public string entName;
            private void Awake()
            {
                entity = GetComponent<BaseCombatEntity>();
                entName = entity.ShortPrefabName.Replace(".deployed", "");
                entName = entName.Replace("_deployed", "");
                Instance.timer.Once(
                    (entity != null && entity as BuildingBlock != null) ? 3f :
                    configDecayDeployable.ContainsKey(entName) ? float.Parse(
                        ((Dictionary<string, object>) configDecayDeployable[entName])["Start Decay After"].ToString()) :
                    3f, () => InvokeHandler.InvokeRepeating(this, DecayEntity, 1f, 1f));
            }

            private bool HasBuildingResources()
            {
                if (entity.GetBuildingPrivilege() != null &&
                    entity.GetBuildingPrivilege().inventory.itemList.Count != 0)
                {
                    if (entity as BuildingBlock != null)
                    {
                        var grade = Instance.GetBuildingGrade((BuildingBlock) entity);
                        switch (grade)
                        {
                            case "Twigs":
                            case "Wood":
                                if (entity.GetBuildingPrivilege().inventory.itemList
                                        .Find(x => x.info.shortname.ToString() == "wood") != null)
                                    return true;
                                break;
                            case "Stone":
                                if (entity.GetBuildingPrivilege().inventory.itemList
                                        .Find(x => x.info.shortname.ToString() == "stones") != null)
                                    return true;
                                break;
                            case "Metal":
                                if (entity.GetBuildingPrivilege().inventory.itemList
                                        .Find(x => x.info.shortname.ToString() == "metal.fragments") != null)
                                    return true;
                                break;
                            case "TopTier":
                                if (entity.GetBuildingPrivilege().inventory.itemList
                                        .Find(x => x.info.shortname.ToString() == "metal.refined") != null)
                                    return true;
                                break;
                        }
                    }

                    if (configDecayDeployable.ContainsKey(entName))
                    {
                        var list = (List<object>) ((Dictionary<string, object>)configDecayDeployable[entName])["ItemList"];
                        if (list != null)
                        {
                            foreach (var item in list)
                            {
                                if (entity.GetBuildingPrivilege() != null && entity.GetBuildingPrivilege().inventory
                                        .itemList.Find(x => x.info.shortname.ToLower() == ((string)item).ToLower()) == null)
                                    return false;
                            }

                            return true;
                        }
                        return true;
                    }
                }

                return false;
            }

            private void DecayEntity()
            {
                if (!HasBuildingResources())
                {
                    if (entity as BuildingBlock != null)
                    {
                        var grade = Instance.GetBuildingGrade(entity as BuildingBlock);
                        if (!configDecayBuild.ContainsKey(grade))
                        {
                            Destroy(this);
                            return;
                        }

                        if (entity.GetBuildingPrivilege() == null)
                        {
                            entity.Hurt(entity.MaxHealth() / float.Parse(((Dictionary<string, object>)configDecayBuild[grade])["Out TC Range"].ToString()), DamageType.Decay);
                        }
                        else
                        {
                            entity.Hurt(entity.MaxHealth() / float.Parse(((Dictionary<string, object>)configDecayBuild[grade])["In TC Range"].ToString()), DamageType.Decay);
                        }

                        return;
                    }

                    if (!configDecayDeployable.ContainsKey(entName.Replace("_", ".")))
                    {
                        Destroy(this);
                        return;
                    }
                    if (entity.GetBuildingPrivilege() == null)
                    {
                        entity.Hurt(entity.MaxHealth() / float.Parse(((Dictionary<string, object>)configDecayDeployable[entName.Replace("_", ".")])["Decay Out TC Range Time"].ToString()), DamageType.Decay);
                    }
                    else
                    {
                        entity.Hurt(entity.MaxHealth() / float.Parse(((Dictionary<string, object>)configDecayDeployable[entName.Replace("_", ".")])["Decay In TC Range Time"].ToString()), DamageType.Decay);
                    }
                }
            }

            void OnDestroy()
            {
                InvokeHandler.CancelInvoke(this, DecayEntity);
                Destroy(this);
            }

        }

        void DeleteAll<T>() where T : MonoBehaviour
        {
            foreach (var type in UnityEngine.Object.FindObjectsOfType<T>())
                UnityEngine.Object.Destroy(type);
        }

        string GetBuildingGrade(BuildingBlock ent)
        {
            string grade = "Twigs";
            switch (ent.grade.GetHashCode())
            {
                case 0:
                    return "Twigs";
                case 1:
                    return "Wood";
                case 2:
                    return "Stone";
                case 3:
                    return "Metal";
                case 4:
                    return "TopTier";
            }

            return grade;


        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity is BuildingPrivlidge) return;
            if (entity.GetComponent<BaseEntity>() != null && entity.GetComponent<BaseEntity>().OwnerID != 0L)
            {
                if (entity.GetComponent<BaseCombatEntity>() == null) return;
                entity.GetOrAddComponent<DeployDecay>();
            }
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            UnityEngine.Object.Destroy(entity.GetComponent<DeployDecay>());
        }

        void build_dep_list()
        {
            deployable_list = new Dictionary<string, ItemDefinition>
            {
                { "barricade.concrete", null },
                { "barricade.metal", null },
                { "barricade.sandbags", null },
                { "barricade.stone", null },
                { "barricade.wood", null },
                { "barricade.woodwire", null },
                { "BBQ.Deployed", null },
                { "beartrap", null },
                { "box.wooden.large", null },
                { "campfire", null },
                { "fridge.deployed", null },
                { "furnace", null },
                { "furnace.large", null },
                { "gates.external.high.wood", null },
                { "jackolantern.angry", null },
                { "jackolantern.happy", null },
                { "landmine", null },
                { "lantern.deployed", null },
                { "locker.deployed", null },
                { "reactivetarget_deployed", null },
                { "refinery_small_deployed", null },
                { "repairbench_deployed", null },
                { "researchtable_deployed", null },
                { "skull_fire_pit", null },
                { "sleepingbag_leather_deployed", null },
                { "spikes.floor", null },
                { "survivalfishtrap.deployed", null },
                { "tunalight.deployed", null },
                { "wall.external.high.stone", null },
                { "wall.external.high.wood", null },
                { "water_catcher_large", null },
                { "water_catcher_small", null },
                { "WaterBarrel", null },
                { "woodbox_deployed", null },
                { "wall.window.glass.reinforced", null }
            };
            if(!deployable_list.ContainsKey("repairbench.deployed"))
                deployable_list.Add("repairbench.deployed", null);
            if (!deployable_list.ContainsKey("refinery.small.deployed"))
                deployable_list.Add("refinery.small.deployed", null);
        }
    }
}