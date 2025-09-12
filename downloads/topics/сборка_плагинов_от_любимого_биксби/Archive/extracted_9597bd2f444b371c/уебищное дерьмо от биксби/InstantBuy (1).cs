namespace Oxide.Plugins
{
    [Info("InstantBuy", "b1xbyy", "1.0.0")]
    public class InstantBuy : RustPlugin
    {
        object OnBuyVendingItem(VendingMachine machine, BasePlayer player, int sellOrderID, int amount)
        {
            if (machine == null || player == null) return null;

            if (player.inventory.containerMain.IsFull() && player.inventory.containerBelt.IsFull()) return null;

            machine.ClientRPC<int>(null, "CLIENT_StartVendingSounds", sellOrderID);
            machine.DoTransaction(player, sellOrderID, amount);
            return false;
        }
    }
}