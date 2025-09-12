using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using UnityEngine;

///Скачано с дискорд сервера Rust Edit [PRO+]
///discord.gg/9vyTXsJyKR

namespace Oxide.Plugins
{
    [Info("Stacks", "discord.gg/9vyTXsJyKR", "1.0.2")]
    [Description("Stacks")]
    public class Stacks : RustPlugin
    {
        #region Fields

        public Dictionary<string, ItemCategory> ItemCategories = new Dictionary<string, ItemCategory>()
        {
            {"Боеприпасы", ItemCategory.Ammunition},
            {"Одежда", ItemCategory.Attire},
            {"Компоненты", ItemCategory.Component},
            {"Конструкции", ItemCategory.Construction},
            {"Еда", ItemCategory.Food},
            {"Предметы", ItemCategory.Items},
            {"Медикаменты", ItemCategory.Medical},
            {"Прочее", ItemCategory.Misc},
            {"Ресурсы", ItemCategory.Resources},
            {"Инструменты", ItemCategory.Tool}, 
            {"Ловушки", ItemCategory.Traps},
            {"Оружие", ItemCategory.Weapon}
        };
        
        #endregion

        #region Hooks
         
        object CanMoveItem(Item item, PlayerInventory inventory, uint container, int slot, uint amount)
        {
            if (item.amount < UInt16.MaxValue) { return null; }

            ItemContainer itemContainer = inventory.FindContainer(container);
            if (itemContainer == null) { return null; }
            
            ItemContainer playerInventory = inventory.GetContainer(PlayerInventory.Type.Main);
            BasePlayer player = playerInventory.GetOwnerPlayer();

            bool aboveMaxStack = false;
            int configAmount =
                _config.SettingsCategory[item.info.category.ToString()][item.info.displayName.translated];

            if (item.amount > configAmount) { aboveMaxStack = true; }
            if (amount + item.amount / UInt16.MaxValue == item.amount % UInt16.MaxValue)
            {
                if (aboveMaxStack)
                {
                    Item item2 = item.SplitItem(configAmount);
                    if (!item2.MoveToContainer(itemContainer, slot, true))
                    {
                        item.amount += item2.amount;
                        item2.Remove(0f);
                    }
                    ItemManager.DoRemoves();
                    inventory.ServerUpdate(0f);
                    return true;
                }
                item.MoveToContainer(itemContainer, slot, true);
                return true;
            }
            else if (amount + (item.amount / 2) / UInt16.MaxValue == (item.amount / 2) % UInt16.MaxValue + item.amount % 2)
            {
                if (aboveMaxStack)
                {
                    Item split;
					if (configAmount > item.amount / 2) { split = item.SplitItem(Convert.ToInt32(item.amount) / 2); }
                    else { split = item.SplitItem(configAmount); }

                    if (!split.MoveToContainer(itemContainer, slot, true))
                    {
                        item.amount += split.amount;
                        split.Remove(0f);
                    }
                    ItemManager.DoRemoves();
                    inventory.ServerUpdate(0f);
                    return true;
                }
                Item item2 = item.SplitItem(item.amount / 2);
				if (!((item.amount + item2.amount) % 2 == 0)) { item2.amount++; item.amount--; }
				
                if (!item2.MoveToContainer(itemContainer, slot, true))
                {
                    item.amount += item2.amount;
                    item2.Remove(0f);
                }
                ItemManager.DoRemoves();
                inventory.ServerUpdate(0f);
                return true;
            }
            else if (item.amount > UInt16.MaxValue && amount != item.amount / 2)
            {
                Item item2;
                if (aboveMaxStack) { item2 = item.SplitItem(configAmount); }
                else { item2 = item.SplitItem(65000); }
                if (!item2.MoveToContainer(itemContainer, slot, true))
                {
                    item.amount += item2.amount;
                    item2.Remove(0f);
                }
                ItemManager.DoRemoves();
                inventory.ServerUpdate(0f);
                return true;
            }
            return null;
        }
        
        void OnServerInitialized()
        {
            PrintWarning("\n-----------------------------\n" +
            "     Author - https://topplugin.ru/\n" +
            "     VK - https://vk.com/rustnastroika\n" +
            "     Discord - https://discord.com/invite/5DPTsRmd3G\n" +
            "-----------------------------");
            foreach (var itemCategory in ItemCategories)
            {
                foreach (var item in ItemManager.itemList.Where(x => x.category.Equals(itemCategory.Value)))
                {
                    if (item.condition.enabled && item.condition.max > 0) continue;

                    if (_config.SettingsCategory.ContainsKey(itemCategory.Key))
                    {
                        if (_config.SettingsCategory[itemCategory.Key].ContainsKey(item.displayName.translated))
                            continue;
                        _config.SettingsCategory[itemCategory.Key].Add(item.displayName.translated, item.stackable);
                        PrintWarning($"Добавили в категорию \"{itemCategory.Key}\" предмет \"{item.displayName.translated}\"");
                    }
                }
            }
            
            foreach (var itemCategory in ItemCategories)
            {
                foreach (var item in ItemManager.itemList.Where(x => x.category.Equals(itemCategory.Value)))
                {
                    if (item.condition.enabled && item.condition.max > 0) continue;
                    
                    item.stackable = _config.SettingsCategory[itemCategory.Key][item.displayName.translated];
                }
            }

            SaveConfig();
            
        }

        #endregion
        
        #region Methods

        

        #endregion

        #region Config

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration()
            {
                SettingsCategory = new Dictionary<string, Dictionary<string, int>>()
            };

            foreach (var itemCategory in ItemCategories)
            {
                if (_config.SettingsCategory.ContainsKey(itemCategory.Key)) continue;
                _config.SettingsCategory.Add(itemCategory.Key, new Dictionary<string, int>());
            }

            foreach (var itemCategory in ItemCategories)
            {
                foreach (var item in ItemManager.itemList.Where(x => x.category.Equals(itemCategory.Value)))
                {
                    if (item.condition.enabled && item.condition.max > 0) continue;

                    if (_config.SettingsCategory.ContainsKey(itemCategory.Key))
                    {
                        if (_config.SettingsCategory[itemCategory.Key].ContainsKey(item.displayName.translated))
                            continue;
                        _config.SettingsCategory[itemCategory.Key].Add(item.displayName.translated, item.stackable);
                    }
                }
            }
        }

        public Configuration _config;

        public class Configuration
        {
            [JsonProperty("Стаки предметов по категориям")]
            public Dictionary<string, Dictionary<string, int>> SettingsCategory = new Dictionary<string, Dictionary<string, int>>();
        }

        #endregion
        
    }
}