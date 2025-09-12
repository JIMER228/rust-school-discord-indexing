namespace Oxide.Plugins
{
    [Info("DeathBackpack", "CryPlugins", "0.0.1")]
    [Description("Turning corpses into a backpack")]
    public class DeathBackpack : RustPlugin
    {
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            var corpse = entity as LootableCorpse;
            if (corpse == null || entity is NPCPlayer) return;

            timer.Once(0.3f, () =>
            {
                corpse.Kill();
                corpse.DropItems();
            });
        }
    }
}