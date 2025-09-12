namespace Oxide.Plugins
{
    [Info("NoBulletFire", "djm.", "1.0.0")]
    [Description("Deletes fire from bullets")]
    class NoBulletFire : RustPlugin 
    {
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity.PrefabName == "assets/bundled/prefabs/fireball_small.prefab" || entity.PrefabName == "assets/bundled/prefabs/fireball_small_shotgun.prefab")
                entity.Kill();
        }
    }
}