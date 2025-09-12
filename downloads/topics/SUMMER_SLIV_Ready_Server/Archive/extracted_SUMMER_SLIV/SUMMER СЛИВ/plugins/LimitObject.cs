using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("LimitObject", "Sparkless", "0.0.1")]
    public class LimitObject : RustPlugin
    {
        public class DataFiles
        {
            public int FoundationLimit;
        }

        public Dictionary<string, DataFiles> DataFile = new Dictionary<string, DataFiles>();
        

        void OnServerInitialized()
        {
            try
            {
                DataFile = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<string, DataFiles>>(Name);
            }
            catch
            {
                DataFile = new Dictionary<string, DataFiles>();
            }
        }

        void Unload()
        {
            if (DataFile != null)
            {
                Interface.Oxide.DataFileSystem.WriteObject(Name, DataFile);
            }
        }

        bool CanPickupEntity(BasePlayer player, BaseEntity entity)
        {
            RemoveProcess(entity, true);
            return true;
        }

        void RemoveProcess(BaseEntity entity, bool pickup)
        {
            if (entity as BuildingPrivlidge)
            {
                if (DataFile.ContainsKey(entity.OwnerID.ToString()))
                {
                    DataFile[entity.OwnerID.ToString()].FoundationLimit -= 1;
                    return;
                }
            }
        }

        string CanRemove(BasePlayer player, BaseEntity entity)
        {
            if (entity == null) return null;
            RemoveProcess(entity, false);
            return null;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            RemoveProcess(entity, false);
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            try
            {
                BasePlayer player = plan.GetOwnerPlayer();
                var entity = go.GetComponent<BaseEntity>();
                if (entity == null) return;
                if (entity as BuildingBlock && entity.GetBuildingPrivilege() != null)
                {
                    var build = entity.GetBuildingPrivilege().GetBuilding();
                    if (build != null)
                    {
                        if (entity.PrefabName == "assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab")
                        {
                            List<DecayEntity> ListEntity = build.decayEntities.ToList().FindAll(entitys => entitys.PrefabName == "assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab");
                            if (ListEntity.Count > 250)
                            {
                                NextTick(() => entity.Kill());
                                player.ChatMessage($"Вы превысили лимит установки фундаментов в одном шкафу! Максимальное количество фундаментов, которое можно установить в одном шкафу: 250");
                            }
                            else
                            {
                                if (player.SecondsSinceAttacked > 5)
                                {
                                    int ostatok = 250 - ListEntity.Count;
                                    player.ChatMessage($"В данном шкафу можно еще поставить <color=#a5e664>{ostatok}</color> фундаментов!");
                                    player.lastAttackedTime = Time.time;
                                }
                            }
                        }
                        
                        if (entity.PrefabName == "assets/prefabs/building core/foundation/foundation.prefab")
                        {
                            List<DecayEntity> ListEntity = build.decayEntities.ToList().FindAll(entitys => entitys.PrefabName == "assets/prefabs/building core/foundation/foundation.prefab");
                            if (ListEntity.Count > 100)
                            {
                                if (entity.PrefabName == "assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab") return;
                                NextTick(() => entity.Kill());
                                player.ChatMessage($"Вы превысили лимит установки фундаментов в одном шкафу! Максимальное количество фундаментов, которое можно установить в одном шкафу: 100");
                            }
                            else
                            {
                                if (player.SecondsSinceAttacked > 5)
                                {
                                    int ostatok = 100 - ListEntity.Count;
                                    player.ChatMessage($"В данном шкафу можно еще поставить <color=#a5e664>{ostatok}</color> фундаментов!");
                                    player.lastAttackedTime = Time.time;
                                }
                            }
                        }
                    }
                }
            } catch { }
        }
    }
}