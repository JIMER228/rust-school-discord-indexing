using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("InstantCraft", "Vlad-00003/Stifler", "1.2.1", ResourceId = 2409)]
    [Description("Instant craft items(includes normalspeed list and blacklist)")]

    class InstantCraft : RustPlugin
    {
        #region Config setup

        PluginConfig config;

        class PluginConfig
        {
            public string prefix { get; set; }
            public string prefixColor { get; set; }
            public List<string> BlockedItems { get; set; }
            public List<string> NormalSpeed { get; set; }
            public bool SplitStacks { get; set; }
        }

        #endregion

        #region Initializing

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            Config["Prefix"] = "InstantCraft";
            Config["Prefix Color"] = "#FF0000";
            Config["Blocked item list"] = new List<string> {};
            Config["Normal Speed"] = new List<string> { "Hammer", "Rock", "Camp Fire" };
            Config["Split Stacks"] = true;
            SaveConfig();
            PrintWarning("New configuration file created.");
        }
        void LoadConfigValues()
        {
            Config.Load();
            config = new PluginConfig
            {
                prefix = Config["Prefix"] as string,
                prefixColor = Config["Prefix Color"] as string,
                BlockedItems = ((IEnumerable)Config["Blocked item list"]).Cast<string>().ToList(),
                NormalSpeed = ((IEnumerable)Config["Normal Speed"]).Cast<string>().ToList(),
                SplitStacks = (bool)Config["Split Stacks"]
            };
        }

        void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"InvFull","В вашем <color=yellow>инвентаре</color> <color=red>нет свободного места!</color>" },
                {"NormalSpeed","Этот предмет <color=red>убран</color> из быстрого крафта." },
                {"Blocked","Крафт данного предмета <color=red>запрещён</color>" },
                {"NotEnoughtSlots","<color=red>Недостаточно слотов</color> для крафта! Создано <color=green>{0}</color>/<color=green>{1}</color>" }
            }, this);
        }

        void Init()
        {
            LoadMessages();
            LoadConfigValues();
        }

        #endregion

        #region Main function
        private object OnItemCraft(ItemCraftTask task)
        {
            var player = task.owner;
            int finalamount = 0;
            bool lastslot = false;
            int refund;
            if (task.amount > task.blueprint.targetItem.stackable)
            {
                SendToChat(player, "Неверное количество, введите не больше " + task.blueprint.targetItem.stackable);
                return null;         
                task.cancelled = true;				
            }
            int amount = task.amount;
            int invAmount = player.inventory.GetAmount(task.blueprint.targetItem.itemid);

            if (task.blueprint.targetItem.shortname == "door.key")
                return null;

            if (FreeSlots(player) <= 0)
            {
                if (invAmount == 0 || invAmount >= task.blueprint.targetItem.stackable)
                {
                    task.cancelled = true;
                    RefundIngredients(task.blueprint, player, task.amount);

                    SendToChat(player, GetMsg("InvFull", player.UserIDString));
                    return null;
                }
                lastslot = true;
            }

            if (config.BlockedItems.Contains(task.blueprint.targetItem.displayName.english) || config.BlockedItems.Contains(task.blueprint.targetItem.shortname))
            {
                task.cancelled = true;
                RefundIngredients(task.blueprint, player, task.amount);

                SendToChat(player, GetMsg("Blocked", player.UserIDString));
                return null;
            }

            if (config.NormalSpeed.Contains(task.blueprint.targetItem.displayName.english) || config.NormalSpeed.Contains(task.blueprint.targetItem.shortname))
            {
                SendToChat(player, GetMsg("NormalSpeed", player.UserIDString));
                return null;
            }

            task.endTime = 1f;
            if (lastslot)
            {
                var spaceleft = task.blueprint.targetItem.stackable - invAmount;
                int cancraft = spaceleft / task.blueprint.amountToCreate;
                refund = amount - cancraft;
                if (refund > 0)
                {
                    string reply = string.Format(GetMsg("NotEnoughtSlots", player.userID), cancraft, amount);
                    SendToChat(player, reply);
                    //GiveItem(player, task.blueprint.targetItem, cancraft * task.blueprint.amountToCreate, (ulong)task.skinID);
                    player.GiveItem(ItemManager.CreateByItemID(task.blueprint.targetItem.itemid, cancraft * task.blueprint.amountToCreate, task.skinID));
                    RefundIngredients(task.blueprint, player, refund);
                    task.cancelled = true;
                    return null;
                }
                //GiveItem(player, task.blueprint.targetItem, amount * task.blueprint.amountToCreate, (ulong)task.skinID);
                player.GiveItem(ItemManager.CreateByItemID(task.blueprint.targetItem.itemid, amount * task.blueprint.amountToCreate, task.skinID));
                task.cancelled = true;
                return null;
            }
            finalamount = amount * task.blueprint.amountToCreate;
            var stacks = CalculateStacks(finalamount, task.blueprint.targetItem);
            if(config.SplitStacks || task.blueprint.targetItem.stackable == 1)
            {
                if(stacks.Count() > FreeSlots(player))
                {
                    int refund_stacks = (stacks.Count() - FreeSlots(player));
                    int refund_amount = refund_stacks * stacks.ElementAt(0) + stacks.Last();
                    refund = refund_amount / task.blueprint.amountToCreate;
                    int iter = FreeSlots(player);
                    int created=0;
                    for(int i = 0; i < iter; i++)
                    {
                        player.GiveItem(ItemManager.CreateByItemID(task.blueprint.targetItem.itemid, stacks.ElementAt(i), task.skinID));
                        created += stacks.ElementAt(i);
                    }
                    RefundIngredients(task.blueprint, player, refund);
                    string reply = string.Format(GetMsg("NotEnoughtSlots", player.userID), created, amount * task.blueprint.amountToCreate);
                    SendToChat(player, reply);
                    task.cancelled = true;
                    return null;
                }
                if(stacks.Count() > 1)
                {
                    foreach(var stack_amount in stacks)
                    {
                        player.GiveItem(ItemManager.CreateByItemID(task.blueprint.targetItem.itemid, stack_amount, task.skinID));
                    }
                    task.cancelled = true;
                    return null;
                }
            }
            player.GiveItem(ItemManager.CreateByItemID(task.blueprint.targetItem.itemid, finalamount, task.skinID));
            task.cancelled = true;
            return null;
        }

        private object OnItemResearch(Item item, BasePlayer player)
        {
            if (config.BlockedItems.Contains(item.info.displayName.english) || config.BlockedItems.Contains(item.info.shortname)) 
            {
                SendToChat(player, GetMsg("Blocked", player.UserIDString));
                return false;
            }
            return null;
        }
        #endregion

        #region Helpers
        //Thanks Norn for this functions and his MagicCraft plugin!
        private IEnumerable<int> CalculateStacks(int amount, ItemDefinition item)
        {
            var results = Enumerable.Repeat(item.stackable, amount / item.stackable); if (amount % item.stackable > 0) { results = results.Concat(Enumerable.Repeat(amount % item.stackable, 1)); }
            return results;
        }
        private void RefundIngredients(ItemBlueprint bp, BasePlayer player, int amount = 1)
        {
            using (List<ItemAmount>.Enumerator enumerator = bp.ingredients.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    ItemAmount current = enumerator.Current;
                    Item i = ItemManager.CreateByItemID(current.itemid, Convert.ToInt32(current.amount) * amount);
                    if (!i.MoveToContainer(player.inventory.containerMain)) { i.Drop(player.eyes.position, player.eyes.BodyForward() * 2f); }
                }
            }
        }

        //Sends the message to the player chat with prefix
        private void SendToChat(BasePlayer Player, string Message)
        {
            PrintToChat(Player, "<color=" + config.prefixColor + ">[" + config.prefix + "]</color> " + Message);
        }

        //Sends the message to the whole chat with prefix
        private void SendToChat(string Message)
        {
            PrintToChat("<color=" + config.prefixColor + ">[" + config.prefix + "]</color> " + Message);
        }

        //Get the msg form lang API
        string GetMsg(string key, object userID = null) => lang.GetMessage(key, this, userID == null ? null : userID.ToString());
        /// <summary>
        /// Get free slots left in player inventory
        /// </summary>
        /// <param name="player"></param>
        /// <returns>int count of the slots</returns>
        int FreeSlots(BasePlayer player) => 30 - player.inventory.containerMain.itemList.Count - player.inventory.containerBelt.itemList.Count;
        #endregion

    }
}