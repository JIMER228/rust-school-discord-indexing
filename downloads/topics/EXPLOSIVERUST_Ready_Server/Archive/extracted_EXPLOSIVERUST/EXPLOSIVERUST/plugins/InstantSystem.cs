using Newtonsoft.Json;
using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("InstantSystem", "https://topplugin.ru / https://discord.com/invite/5DPTsRmd3G", "1.0.3")]
    public class InstantSystem : RustPlugin
    { 
        #region Класс
        public class InstaSetting 
        {
            [JsonProperty("Проверять ли наличие свободного места")] public bool InventoryCheck;
            [JsonProperty("Список предметов, крафт которых будет с обычной скростью")] public List<string> ItemCraftList;
            [JsonProperty("Черный список")] public List<string> BlackList;
        }
        #endregion

        #region Конфиг
        Configuration config;
        class Configuration
        {
            [JsonProperty("Настройка крафта")] public InstaSetting craft = new InstaSetting();
            public static Configuration GetNewCong()
            {
                return new Configuration
                {
                    craft = new InstaSetting()
                    {
                        InventoryCheck = true,
                        ItemCraftList = new List<string>()
                        {
                            "rock",
                            "hammer"
                        },
                        BlackList = new List<string>()
                        {
                            "rock"
                        }
                    }
                };
            }
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.craft == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewCong();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Хуки
        private object OnItemCraft(ItemCraftTask item)
        {
            return OnCraft(item);
        }
        #endregion

        #region Коре
        private object OnCraft(ItemCraftTask task)
        {
            if (task.cancelled == true)
            {
                return null;
            }
            
            var player = task.owner;
            var target = task.blueprint.targetItem;
            var targetName = target.shortname;

            if (targetName.Contains("key"))
            {
                return null;
            }

            if (IsBlocked(targetName))
            {
                task.cancelled = true;
                SendReply(player, "<size=12>Крафт данного <color=#ee3e61>предмета</color> заблокирован!</size>");
                GiveRefund(player, task.takenItems);
                return null;
            }

            var stacks = GetStacks(target, task.amount * task.blueprint.amountToCreate);
            var slots = FreeSlots(player);

            if (HasPlace(slots, stacks) == false)
            {
                task.cancelled = true;
                SendReply(player, "<size=12>У вас <color=#ee3e61>недостаточно</color> места в инвентаре!</size>");
                GiveRefund(player, task.takenItems);
                return null;
            }
            
            if (IsNormalItem(targetName))
            {
                SendReply(player, "<size=12>Данный предмет будет <color=#ee3e61>крафтиться</color> с обычной скоростью!</size>");
                return null;
            }
            
            GiveItem(player, task, target, stacks, task.skinID);
            task.cancelled = true;
            return null;
        }

        private void GiveItem(BasePlayer player, ItemCraftTask task, ItemDefinition def, List<int> stacks, int taskSkinID)
        {
            var skin = ItemDefinition.FindSkin(def.itemid, taskSkinID);
            
            foreach (var stack in stacks)
            {
                var item = ItemManager.Create(def, stack, skin);
                player.GiveItem(item);
                Interface.CallHook("OnItemCraftFinished", task, item);
            }
        }

        private int FreeSlots(BasePlayer player)
        {
            var slots = player.inventory.containerMain.capacity + player.inventory.containerBelt.capacity;
            var taken = player.inventory.containerMain.itemList.Count + player.inventory.containerBelt.itemList.Count;
            return slots - taken;
        }

        private void GiveRefund(BasePlayer player, List<Item> items)
        {
            foreach (var item in items)
            {
                player.GiveItem(item);
            }
        }

        private List<int> GetStacks(ItemDefinition item, int amount) 
        {
            var list = new List<int>();
            var maxStack = item.stackable;

            if (maxStack == 0)
            {
                maxStack = 1;
            }

            while (amount > maxStack)
            {
                amount -= maxStack;
                list.Add(maxStack);
            }
            
            list.Add(amount);
            
            return list; 
        }

        private bool IsNormalItem(string name)
        {
            return config.craft.ItemCraftList?.Contains(name) ?? false;
        }

        private bool IsBlocked(string name)
        {
            return config.craft.BlackList?.Contains(name) ?? false;
        }

        private bool HasPlace(int slots, List<int> stacks)
        {
            if (config.craft.InventoryCheck == false)
            {
                return true;
            }

            return slots > 0;
        }

        #endregion
    }
}