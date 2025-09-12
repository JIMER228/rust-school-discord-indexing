using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("LimitObject", "Sparkless", "0.0.1")]
    public class LimitObject : RustPlugin
    {
        private ConfigData _config;

        public class DataFiles
        {
            public int CupboardLimit;
        }

        public Dictionary<string, DataFiles> DataFile = new Dictionary<string, DataFiles>();

        class ConfigData
        {

            [JsonProperty("Максимальное количество объектов, которое можно поставить в 1 шкафу")]
            public int AmountAllObjectMax = 12500;
        }

        protected override void LoadDefaultConfig()
        {
            _config = new ConfigData();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigData>();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

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
                    DataFile[entity.OwnerID.ToString()].CupboardLimit -= 1;
                    return;
                }
            }
        }




        [PluginReference] private Plugin Clans;

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
                        List<DecayEntity> ListEntity =
                            build.decayEntities.ToList().FindAll(entitys => entitys as BuildingBlock);
                        if (ListEntity.Count > _config.AmountAllObjectMax)
                        {
                            NextTick(() => entity.Kill());
                            player.ChatMessage(
                                $"Вы превысили лимит установки объектов в 1 шкафу! Максимальное количество объектов, которое можно установить в 1 шкафу - {_config.AmountAllObjectMax}");
                        }
                        else
                        {
                            if (player.SecondsSinceAttacked > 5)
                            {
                                int ostatok = _config.AmountAllObjectMax - ListEntity.Count;
                                player.ChatMessage($"В данном шкафу можно еще поставить <color=#a5e664>{ostatok}</color> объектов!");
                                player.lastAttackedTime = UnityEngine.Time.time;
                            }
                        }
                    }
                }
            }
            catch
            {
                
            }
        }
    }
}