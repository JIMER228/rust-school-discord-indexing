using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("NoConveyor", "REIN", "1.1.0")]
    public class NoConveyor : RustPlugin
    {
        private readonly HashSet<string> restrictedPrefabs = new HashSet<string>
        {
            "assets/prefabs/deployable/conveyor/industrial.conveyor.deployed.prefab",
            "assets/prefabs/deployable/conveyor/industrial.splitter.deployed.prefab",
            "assets/prefabs/deployable/conveyor/industrial.combiner.deployed.prefab"
        };

        private readonly HashSet<string> blockedItems = new HashSet<string>
        {
            "industrial.conveyor",
            "industrial.splitter",
            "industrial.combiner"
        };

        private object OnItemDeployed(Deployer deployer, BaseEntity entity, BasePlayer player)
        {
            if (entity == null || player == null) return null;

            if (restrictedPrefabs.Contains(entity.PrefabName))
            {
                SendMessage(player);
                return true;
            }
            return null;
        }

        private object CanPickupEntity(BasePlayer player, BaseCombatEntity entity)
        {
            if (entity == null || player == null) return null;

            var item = entity.GetItem();
            if (item != null && blockedItems.Contains(item.info.shortname))
            {
                SendMessage(player);
                return false;
            }
            return null;
        }

        private object CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainer, int targetSlot, int amount, ItemMoveModifier modifiers)
        {
            if (item?.info == null || playerLoot == null) return null;
            
            if (blockedItems.Contains(item.info.shortname))
            {
                if (targetSlot >= 0 && targetSlot <= 5)
                {
                    var player = playerLoot.GetComponent<BasePlayer>();
                    if (player != null)
                    {
                        SendMessage(player);
                    }
                    return false;
                }
            }
            return null;
        }

        private object CanLootEntity(BasePlayer player, StorageContainer entity)
        {
            if (entity == null || player == null) return null;

            var item = entity.GetItem();
            if (item != null && blockedItems.Contains(item.info.shortname))
            {
                SendMessage(player);
                return false;
            }
            return null;
        }

        private object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (item?.info == null || player == null) return null;
            
            if (blockedItems.Contains(item.info.shortname))
            {
                // Блокируем взятие в руки
                if (action == "equip")
                {
                    SendMessage(player);
                    return false;
                }
            }
            return null;
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (item?.info == null || container == null || !blockedItems.Contains(item.info.shortname)) return;

            // Проверяем, является ли контейнер поясом (слотами 0-5)
            if (container.playerOwner?.inventory?.containerBelt == container)
            {
                // Перемещаем предмет в основной инвентарь
                var mainContainer = container.playerOwner?.inventory?.containerMain;
                if (mainContainer != null)
                {
                    item.MoveToContainer(mainContainer);
                    SendMessage(container.playerOwner);
                }
            }
        }

        private void SendMessage(BasePlayer player)
        {
            Player.Message(player, "<color=#FF0000>ВРЕМЕННО НЕДОСТУПНО</color>");
        }
    }
} 