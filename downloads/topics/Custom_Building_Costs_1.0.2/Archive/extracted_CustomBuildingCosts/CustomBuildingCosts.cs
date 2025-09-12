using Facepunch;
using HarmonyLib;
using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Custom Building Costs", "0xF", "1.0.2")]
    [Description("Allows you to change the cost and items for building and upgrading buildings")]
    public class CustomBuildingCosts : RustPlugin
    {
        #region Defines
        public static CustomBuildingCosts Plugin;
        private static Harmony _harmony;
        static readonly Type buildingEnumType = typeof(BuildingGrade.Enum);
        #endregion

        #region Hooks

        void Init()
        {
            Plugin = this;
        }

        private void Loaded()
        {
            HarmonyInstance.Patch(AccessTools.Method(typeof(ConstructionGrade), "CostToBuild"), new HarmonyMethod(typeof(Patches), "CostToBuildPrefix"));
        }


        void OnStructureUpgrade(BaseCombatEntity entity, BasePlayer player, BuildingGrade.Enum grade)
        {
            player.inventory.SendUpdatedInventory(PlayerInventory.Type.Wear, player.inventory.containerWear, false);
        }

        bool CanAffordUpgrade(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade, ulong skin)
        {
            bool @result = true;
            var cost = block.blockDefinition.GetGrade(grade, skin).CostToBuild(block.grade);
            foreach (global::ItemAmount itemAmount in cost)
            {
                if ((float)player.inventory.GetAmount(itemAmount.itemid) < itemAmount.amount)
                {
                    using (ItemAmountList itemAmountList = global::ItemAmount.SerialiseList(cost))
                    {
                        player.ClientRPCPlayer<ItemAmountList>(null, player, "Client_OnRepairFailedResources", itemAmountList);
                    }
                    @result = false;
                    break;
                }
            }
            SendFakeResourcesToClient(player);
            return @result;
        }

        void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (newItem != null && newItem.info.shortname == "hammer")
            {
                timer.Once(0.1f, () => SendFakeResourcesToClient(player));
            }
            else if (oldItem != null && oldItem.info.shortname == "hammer")
                ReturnToNormal(player);
        }

        #endregion

        #region Methods

        void SendFakeResourcesToClient(BasePlayer player)
        {
            ItemContainer newContainer = new ItemContainer();
            newContainer.capacity = player.inventory.containerWear.capacity + 4;
            newContainer.itemList.AddRange(player.inventory.containerWear.itemList);
            newContainer.itemList.Add(new global::Item
            {
                info = ItemManager.FindItemDefinition("wood"),
                amount = 200,
                position = 10000
            });
            newContainer.itemList.Add(new global::Item
            {
                info = ItemManager.FindItemDefinition("stones"),
                amount = 300,
                position = 10001
            });
            newContainer.itemList.Add(new global::Item
            {
                info = ItemManager.FindItemDefinition("metal.fragments"),
                amount = 200,
                position = 10002
            });
            newContainer.itemList.Add(new global::Item
            {
                info = ItemManager.FindItemDefinition("metal.refined"),
                amount = 25,
                position = 10003
            });
            player.inventory.SendUpdatedInventory(PlayerInventory.Type.Wear, newContainer, false);
        }

        void ReturnToNormal(BasePlayer player)
        {
            player.inventory.SendUpdatedInventory(PlayerInventory.Type.Wear, player.inventory.containerWear, false);
        }
        #endregion

        #region Harmony
        private static class Patches
        {
            internal static bool CostToBuildPrefix(ConstructionGrade __instance, BuildingGrade.Enum fromGrade, ref List<ItemAmount> __result)
            {
                var _costToBuild = new List<global::ItemAmount>();
                float num = (fromGrade == __instance.gradeBase.type) ? 0.2f : 1f;
                foreach (KeyValuePair<string, float> itemAmount in config.build[$"{Enum.GetName(buildingEnumType, __instance.gradeBase.type)}:{__instance.gradeBase.skin}"])
                {
                    ItemDefinition itemDef = ItemManager.FindItemDefinition(itemAmount.Key);
                    if (itemDef == null)
                    {
                        Plugin.PrintWarning($"ItemDefinition with shortname \"{itemAmount.Key}\" not found, skipped.");
                        continue;
                    }
                    if (itemDef.itemid == default(int))
                    {
                        Plugin.PrintWarning($"ItemDefinition with shortname \"{itemAmount.Key}\" haven't itemid, skipped.");
                        continue;
                    }
                    _costToBuild.Add(new global::ItemAmount(itemDef, Mathf.Ceil(itemAmount.Value * __instance.construction.costMultiplier * num)));
                }
                __result = _costToBuild;
                return false;
            }
        }
        #endregion

        #region Config
        static Configuration config;
        public class Configuration
        {
            [JsonProperty(PropertyName = "Cost Settings")]
            public Dictionary<string, Dictionary<string, float>> build = new Dictionary<string, Dictionary<string, float>>();

            public void Prepare()
            {
                var buildingBlock = FileSystem.Load<GameObject>("assets/prefabs/building core/foundation/foundation.prefab").GetComponent<BuildingBlock>();
                var blockDefinition = global::PrefabAttribute.server.Find<global::Construction>(buildingBlock.prefabID);
                foreach (var grade in blockDefinition.grades)
                {
                    string fieldName = $"{Enum.GetName(buildingEnumType, grade.gradeBase.type)}:{grade.gradeBase.skin}";
                    if (build.ContainsKey(fieldName)) continue;
                    build.Add(fieldName, grade.gradeBase.baseCost.ToDictionary(c => c.itemDef.shortname, c => c.amount));
                }
            }
            public static Configuration DefaultConfig()
            {
                var config = new Configuration();
                config.Prepare();
                return config;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
                config.Prepare();
                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion
    }
}