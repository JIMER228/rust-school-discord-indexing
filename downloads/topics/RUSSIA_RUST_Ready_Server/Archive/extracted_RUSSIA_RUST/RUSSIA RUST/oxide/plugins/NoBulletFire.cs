namespace Oxide.Plugins
{
///Скачано с дискорд сервера Rust Edit [PRO+]
///discord.gg/9vyTXsJyKR

    [Info("NoBulletFire", "discord.gg/9vyTXsJyKR.", "1.0.0")]
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