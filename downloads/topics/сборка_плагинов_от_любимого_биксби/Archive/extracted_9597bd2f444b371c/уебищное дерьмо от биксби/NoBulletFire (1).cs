using System.Collections.Generic;
namespace Oxide.Plugins
{
    [Info("NoBulletFire", "b1xbyy", "1.0.0")]
    class NoBulletFire : RustPlugin
    {
        private static readonly HashSet<string> FireballPrefabs = new HashSet<string>
        {
            "assets/prefabs/weapons/flamethrower/flamethrower_fireball.prefab",
            "assets/prefabs/npc/m2bradley/oilfireball2.prefab",
            "assets/prefabs/npc/flame turret/flameturret_fireball.prefab",
            "assets/bundled/prefabs/fireball.prefab",
            "assets/bundled/prefabs/fireball_small.prefab",
            "assets/bundled/prefabs/fireball_small_arrow.prefab",
            "assets/bundled/prefabs/fireball_small_molotov.prefab",
            "assets/bundled/prefabs/fireball_small_shotgun.prefab",
            "assets/bundled/prefabs/napalm.prefab",
            "assets/bundled/prefabs/oilfireballsmall.prefab"
        };

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity is BaseEntity baseEntity && FireballPrefabs.Contains(baseEntity.PrefabName))
            {
                baseEntity.Kill();
            }
        }

        void OnEntitySpawned(Minicopter entity)
        {
            entity.explosionEffect.guid = null;
            entity.fireBall.guid = null;
            entity.serverGibs.guid = null;
        }

        void OnEntitySpawned(ScrapTransportHelicopter entity)
        {

            entity.explosionEffect.guid = null;
            entity.fireBall.guid = null;
            entity.serverGibs.guid = null;
        }

        void OnEntitySpawned(AttackHelicopter entity)
        {
            entity.explosionEffect.guid = null;
            entity.fireBall.guid = null;
            entity.serverGibs.guid = null;
        }
    }
}