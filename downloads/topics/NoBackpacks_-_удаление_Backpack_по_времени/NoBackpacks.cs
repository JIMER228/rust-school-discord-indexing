namespace Oxide.Plugins
{
    [Info("NoBackpacks", "cheese - lox", "1.0.0")]
    class NoBackpacks : RustPlugin
    {
        private void OnEntitySpawned(DroppedItemContainer backpack)
        {
            if (backpack.ShortPrefabName == "item_drop_backpack") 
            backpack.ResetRemovalTime(300.0f);
        }
    }
}