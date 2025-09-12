using Newtonsoft.Json;
using Oxide.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NSmeltSpeed", "North", "1.0.3")]
    public class NSmeltSpeed : RustPlugin
    {
        private ConfigData config;

        private class ConfigData
        {
            [JsonProperty("Настройка скорости плавки")]
            public Dictionary<string, float> SmeltingSpeedSettings { get; set; } = new Dictionary<string, float>
            {
                { "furnace", 2 },
                { "furnace.large", 5 },
                { "electricfurnace.deployed", 3 },
                { "refinery_small_deployed", 4 },
                { "bbq.deployed", 1.5f },
                { "campfire", 1.5f },
                { "hobobarrel.deployed", 2 },
                { "skull_fire_pit", 12 },
                { "fireplace.deployed", 1.5f },
                { "lantern.deployed", 1.5f },
                { "small_refinery_static", 1.5f }
            };

            [JsonProperty("Рейт плавки дерева")]
            public float CharcoalRate { get; set; } = 2.0f;
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<ConfigData>();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        private void OnServerInitialized()
        {
            UpdateExistingElectricFurnaces();
        }

        private void UpdateExistingElectricFurnaces()
        {
            foreach (BaseEntity entity in BaseNetworkable.serverEntities)
            {
                if (entity == null || entity.IsDestroyed) continue;

                if (entity.ShortPrefabName == "electricfurnace.deployed")
                {
                    BaseOven oven = entity as BaseOven;
                    if (oven != null)
                    {
                        if (config.SmeltingSpeedSettings.TryGetValue("electricfurnace.deployed", out float multiplier))
                        {
                            oven.smeltSpeed = (int)Mathf.Max(1, multiplier);
                            oven.SendNetworkUpdateImmediate();
                        }
                    }
                }
            }
        }

        private object OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (oven == null) return null;

            string shortName = oven.ShortPrefabName;

            if (!config.SmeltingSpeedSettings.TryGetValue(shortName, out float multiplier))
            {
                return null;
            }

            oven.smeltSpeed = (int)Mathf.Max(1, multiplier);
            oven.SendNetworkUpdateImmediate();

            return null;
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
            if (entity == null) return;

            string shortName = entity.ShortPrefabName;
            if (string.IsNullOrEmpty(shortName)) return;

            if (config.SmeltingSpeedSettings.TryGetValue(shortName, out float multiplier))
            {
                BaseOven oven = entity as BaseOven;
                if (oven != null)
                {
                    oven.smeltSpeed = (int)Mathf.Max(1, multiplier);
                    oven.SendNetworkUpdateImmediate();
                }
            }
        }

        private void OnFuelConsumed(BaseOven oven, Item fuel, ItemModBurnable burnable)
{
    if (oven == null || fuel == null || burnable == null) return;

    // Проверяем, что топливо - это дерево
    if (fuel.info.shortname == "wood")
    {
        float rate = config.CharcoalRate; // Рейт угля
        int baseFuelRate = oven.GetFuelRate(); // Базовый расход топлива
        int newFuelRate = Mathf.CeilToInt(baseFuelRate * rate); // Учитываем множитель рейта

        // Если у топлива есть побочный продукт (обычно уголь)
        if (burnable.byproductItem != null && burnable.byproductItem.shortname == "charcoal")
        {
            int byproductBaseAmount = burnable.byproductAmount;
            int charcoalAmount = Mathf.CeilToInt(byproductBaseAmount * rate);

            if (charcoalAmount > 0)
            {
                Item charcoal = ItemManager.Create(burnable.byproductItem, charcoalAmount, 0);

                // Перемещение угля в инвентарь печи, если возможно
                if (!charcoal.MoveToContainer(oven.inventory))
                {
                    charcoal.Drop(oven.transform.position, Vector3.zero);
                }
            }
        }

        // Сжигаем топливо по новой формуле
        fuel.UseItem(newFuelRate - baseFuelRate);
    }
}

    }
}
