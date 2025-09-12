using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Vending In Stock", "AVOcoder", "1.0.7")]
    [Description("VendingMachines sell-orders always in stock")]
    class VendingInStock : RustPlugin
    {
        private void OnNpcGiveSoldItem(NPCVendingMachine vm, Item soldItem, BasePlayer buyer)
        {
            if (vm == null || soldItem == null)
                return;

            if (Interface.CallHook("CanVendingStockRefill", vm, soldItem, buyer) != null)
            {
                return;
            }

            Item item = ItemManager.Create(soldItem.info, soldItem.amount, soldItem.skin);
            if (soldItem.blueprintTarget != 0)
            {
                item.blueprintTarget = soldItem.blueprintTarget;
            }

            NextTick(() =>
            {
                if (item == null)
                    return;

                if (vm == null || vm.IsDestroyed) {
                    item.Remove(0f);
                    return;
                }

                vm.transactionActive = true;
                if (!item.MoveToContainer(vm.inventory, -1, true))
                {
                    item.Remove(0f);
                }

                vm.transactionActive = false;
                vm.FullUpdate();
            });
        }
    }
}