using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("CraftSystem", "wazzzup", "1.1.0")]
    [Description("CraftSystem")]
    public class CraftSystem : RustPlugin
    {
        ConfigData configData;
        class ConfigData
        {
            [JsonProperty("общий множитель рейта на крафт 0.0 - 1.0 или более общего времени крафта любого предмета")]
            public float overallRateInPersent = 1.0f;
            [JsonProperty("запретить крафтить, если инвентарь полный")]
            public bool denyOnFullInv = false;
            [JsonProperty("сообщать игроку что инвентарь полный при запрете крафта")]
            public bool messageOnFullInv = true;
            [JsonProperty("включить инстакрафт, если время крафта выставлено в 0")]
            public bool instaCraft = false;
            [JsonProperty("стандартное время время крафта в секундах")]
            public Dictionary<string, float> defaultRates = new Dictionary<string, float>();
            [JsonProperty("индивидуальное время крафта в секундах для предметов, если задано для предмета, игнорится общий рейт")]
            public Dictionary<string, float> myRates = new Dictionary<string, float>();
        }
        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData();
            SaveConfig(configData);
        }
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "invFull", "Your inventory is full u cannot craft" },
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "invFull", "В твоём инвентаре недостаточно места для крафта" },
            }, this, "ru");
        }

        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);

        void Init()
        {
            configData = Config.ReadObject<ConfigData>();
            SaveConfig(configData);
            if (!(configData.denyOnFullInv || configData.instaCraft)) Unsubscribe(nameof(OnItemCraft));
        }
        void OnNewSave()
        {
            configData.defaultRates.Clear();
            SaveConfig(configData);
        }

        void OnServerInitialized()
        {
            var mcITEMS = ItemManager.itemList.ToDictionary(i => i.shortname);
            foreach (var definition in mcITEMS)
            {
                if (definition.Value.Blueprint == null)
                {
                    continue;
                }

                if (!configData.defaultRates.ContainsKey(definition.Key))
                {
                    configData.defaultRates.Add(definition.Key,definition.Value.Blueprint.time);
                }
                if (configData.myRates.ContainsKey(definition.Key))
                {
                    definition.Value.Blueprint.time = configData.myRates[definition.Key];
                }
                else if (configData.overallRateInPersent!=1.0)
                {
                    definition.Value.Blueprint.time = configData.defaultRates[definition.Key] * configData.overallRateInPersent;
                }
            }
            SaveConfig(configData);
        }
        private object OnItemCraft(ItemCraftTask task, BasePlayer crafter)
        {
            int amount = task.amount;
            int stackable = task.blueprint.targetItem.stackable;
            int final_amount = task.blueprint.amountToCreate * amount;
            int total_stacks =  (int)Math.Ceiling((float)final_amount /stackable);
            int invSlots = 30 -crafter.inventory.containerMain.itemList.Count - crafter.inventory.containerBelt.itemList.Count;
            if (invSlots < total_stacks)
            {
                if (configData.messageOnFullInv) SendReply(crafter, lang.GetMessage("invFull", this, crafter.UserIDString));
                foreach (Item takenItem in task.takenItems)
                {
                    if (takenItem != null && takenItem.amount > 0)
                    {
                        if (takenItem.amount > 0 && !takenItem.MoveToContainer(task.owner.inventory.containerMain, -1, true))
                        {
                            takenItem.Drop(crafter.inventory.containerMain.dropPosition + UnityEngine.Random.value * Vector3.down + UnityEngine.Random.insideUnitSphere, crafter.inventory.containerMain.dropVelocity, new Quaternion());
                        }
                    }
                }
                return false;
            }
            if (configData.instaCraft)
            {
                if (total_stacks == 1)
                {
                    Item item = ItemManager.CreateByItemID(task.blueprint.targetItem.itemid, final_amount, (ulong)task.skinID);
                    if (item != null) crafter.GiveItem(item);                }
                else
                {
                    for (int i = 1; i <= total_stacks; i++)
                    {
                        int am = final_amount < stackable ? final_amount : stackable;
                        final_amount -= stackable;
                        Item item = ItemManager.CreateByItemID(task.blueprint.targetItem.itemid, am, (ulong)task.skinID);
                        if (item != null) crafter.GiveItem(item);
                    }
                }
                return false;
            }
            return null;
        }
    }
}
