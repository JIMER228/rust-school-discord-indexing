namespace Oxide.Plugins
{
    [Info("NoWeaponDrop", "b1xbyy", "1.0.0")]
    class NoWeaponDrop : RustPlugin
    {
        private object CanDropActiveItem(BasePlayer player)
        {
            if (player.IsNpc)
            return null;
            return false;
        }
    }
}