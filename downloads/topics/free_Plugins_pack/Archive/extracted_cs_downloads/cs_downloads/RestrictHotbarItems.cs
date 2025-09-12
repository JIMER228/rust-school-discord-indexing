using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("RestrictHotbarItems", "YourName", "1.0.9")]
    [Description("Blocks specific items from hotbar, usage, and removes ammo from weapons.")]
    public class RestrictHotbarItems : RustPlugin
    {
        // Список запрещенных предметов (shortname)
        private readonly string[] restrictedItems = new[]
        {
            "rifle.l96",           // L96
            "lmg.m249",            // M249
            "turret_auto",         // Автоматическая турель
            "guntrap",             // Гантрап
            "flameturret"          // Огненная турель
        };

        // Список пулеметов, у которых нужно удалять патроны
        private readonly string[] weaponsWithAmmo = new[]
        {
            "rifle.l96",
            "lmg.m249"
        };

        // Хук, срабатывающий при попытке переместить предмет
        private object CanMoveItem(Item item, PlayerInventory inventory, ItemContainerId targetContainer, int targetSlot)
        {
            // Проверяем, является ли контейнер горячей панелью
            if (targetContainer == inventory.containerBelt.uid && targetSlot >= 0 && targetSlot < 6)
            {
                // Проверяем, входит ли предмет в список запрещенных
                foreach (string restrictedItem in restrictedItems)
                {
                    if (item.info.shortname == restrictedItem)
                    {
                        // Отправляем красное уведомление и эффект
                        BasePlayer player = inventory._baseEntity as BasePlayer;
                        if (player != null)
                        {
                            player.SendConsoleCommand("ui.notify", 0, $"<color=red>Нельзя помещать {item.info.displayName.english} в горячую панель!</color>");
                            Effect.client.Run("assets/prefabs/locks/keypad/effects/lock-code-denied.prefab", player.transform.position);
                        }

                        // Перемещаем предмет в инвентарь, если он уже в горячей панели
                        if (item.parent == inventory.containerBelt)
                        {
                            item.MoveToContainer(inventory.containerMain);
                        }

                        // Запрещаем перемещение
                        return false;
                    }
                }
            }
            return null;
        }

        // Хук, срабатывающий при попытке выбрать предмет в качестве активного
        private object OnActiveItemChange(BasePlayer player, Item item)
        {
            // Проверяем, что предмет существует
            if (item == null || item.info == null)
                return null;

            // Проверяем, входит ли предмет в список запрещенных
            foreach (string restrictedItem in restrictedItems)
            {
                if (item.info.shortname == restrictedItem)
                {
                    // Отправляем красное уведомление и эффект
                    player.SendConsoleCommand("ui.notify", 0, $"<color=red>Нельзя взять в руки {item.info.displayName.english}!</color>");
                    Effect.client.Run("assets/prefabs/locks/keypad/effects/lock-code-denied.prefab", player.transform.position);

                    // Удаляем патроны, если это оружие
                    foreach (string weapon in weaponsWithAmmo)
                    {
                        if (item.info.shortname == weapon)
                        {
                            ItemModProjectile mod = item.info.GetComponent<ItemModProjectile>();
                            if (mod != null && item.contents != null)
                            {
                                foreach (Item ammo in item.contents.itemList.ToArray())
                                {
                                    ammo.Remove();
                                }
                            }
                        }
                    }

                    // Перемещаем предмет из горячей панели в инвентарь
                    if (item.parent == player.inventory.containerBelt)
                    {
                        item.MoveToContainer(player.inventory.containerMain);
                    }

                    // Запрещаем активацию
                    return false;
                }
            }
            return null;
        }

        // Хук, срабатывающий при попытке атаковать (стрелять)
        private object OnPlayerAttack(BasePlayer player, HitInfo info)
        {
            // Проверяем, что игрок держит предмет
            Item activeItem = player.GetActiveItem();
            if (activeItem == null || activeItem.info == null)
                return null;

            // Проверяем, является ли активный предмет запрещенным оружием
            foreach (string weapon in weaponsWithAmmo)
            {
                if (activeItem.info.shortname == weapon)
                {
                    // Отправляем красное уведомление и эффект
                    player.SendConsoleCommand("ui.notify", 0, $"<color=red>Нельзя стрелять из {activeItem.info.displayName.english}!</color>");
                    Effect.client.Run("assets/prefabs/locks/keypad/effects/lock-code-denied.prefab", player.transform.position);

                    // Удаляем патроны
                    ItemModProjectile mod = activeItem.info.GetComponent<ItemModProjectile>();
                    if (mod != null && activeItem.contents != null)
                    {
                        foreach (Item ammo in activeItem.contents.itemList.ToArray())
                        {
                            ammo.Remove();
                        }
                    }

                    // Перемещаем предмет из горячей панели в инвентарь
                    if (activeItem.parent == player.inventory.containerBelt)
                    {
                        activeItem.MoveToContainer(player.inventory.containerMain);
                    }

                    // Запрещаем стрельбу
                    return false;
                }
            }
            return null;
        }
    }
}