using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BloodBagBear", "YourName", "1.0.0")]
    [Description("Выдаёт пакет с кровью при добыче медведя и автоподъём при повалении.")]
    public class BloodBagBear : RustPlugin
    {
        #region Config
        private ConfigData config;
        class ConfigData
        {
            public float DropChance = 10.0f;
            public string ItemShortName = "blood";
            public string ItemDisplayName = "Пакет с кровью";
            public ulong ItemSkin = 0;
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
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GotBloodBag"] = "Вы получили <color=#ff0000>Пакет с кровью</color>!",
                ["UsedBloodBag"] = "<color=#ff0000>Пакет с кровью</color> спас вас от смерти!",
            }, this);
        }
        #endregion

        #region Hooks
        void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            var player = entity.ToPlayer();
            if (player == null || dispenser == null || item == null) return;
            if (dispenser == null || dispenser.gameObject == null) return;
            // Проверяем, что добывается медведь
            if (dispenser.gameObject.name.Contains("bear", StringComparison.OrdinalIgnoreCase))
            {
                if (UnityEngine.Random.Range(0f, 100f) < config.DropChance)
                {
                    var def = ItemManager.FindItemDefinition(config.ItemShortName);
                    if (def == null)
                    {
                        PrintError($"Item shortname '{config.ItemShortName}' не найден!");
                        return;
                    }
                    var bloodBag = ItemManager.Create(def, 1, config.ItemSkin);
                    if (!string.IsNullOrEmpty(config.ItemDisplayName))
                        bloodBag.name = config.ItemDisplayName;
                    player.GiveItem(bloodBag);
                    player.ChatMessage(lang.GetMessage("GotBloodBag", this, player.UserIDString));
                }
            }
        }

        void OnPlayerWound(BasePlayer player, HitInfo info)
        {
            if (player == null) return;
            var def = ItemManager.FindItemDefinition(config.ItemShortName);
            if (def == null) return;
            // Ищем предмет в инвентаре (main, belt, wear)
            Item found = player.inventory.FindItemByItemID(def.itemid);
            if (found != null)
            {
                found.UseItem(); // Удаляем предмет
                timer.Once(0.1f, () =>
                {
                    if (player.IsWounded())
                    {
                        player.StopWounded();
                        player.ChatMessage(lang.GetMessage("UsedBloodBag", this, player.UserIDString));
                    }
                });
            }
        }
        #endregion
    }
} 