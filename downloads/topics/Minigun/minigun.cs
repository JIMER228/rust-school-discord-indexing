using System.Collections.Generic;
using UnityEngine;
using Rust;

namespace Oxide.Plugins
{
    [Info("minigun", "sdapro", "1.0.0")]
    public class minigun : RustPlugin
    {
        private Dictionary<BasePlayer, int> shotCounts = new Dictionary<BasePlayer, int>();

        private void Init()
        {
            permission.RegisterPermission("minigun.rs", this);
        }

        void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod, ProtoBuf.ProjectileShoot projectiles)
        {
            var weapon = projectile?.GetItem()?.info;
            if (weapon == null) { return; }
            var name = weapon.shortname;

            if (name != "minigun") { return; }
            if (permission.UserHasPermission(player.UserIDString, "minigun.rs"))
            {
                var entity = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/rocket_heli.prefab",
                        player.eyes.position + player.eyes.BodyForward().normalized,
                        player.eyes.rotation) as BaseEntity;
                ServerProjectile sProjectile = entity?.GetComponent<ServerProjectile>();
                sProjectile.InitializeVelocity(player.eyes.HeadForward().normalized * 150f);
                sProjectile.gravityModifier = 0.8f;
                sProjectile.speed = 0;
                entity.creatorEntity = (BaseEntity)player;
                entity.OwnerID = player.userID;
                entity.Spawn();
                TimedExplosive explosive = entity.GetComponent<TimedExplosive>();

                int shotCount;
                if (shotCounts.TryGetValue(player, out shotCount))
                {
                    shotCounts[player] = shotCount + 1;
                    if (shotCount + 1 >= 200)
                    {
                        RemoveM249(player);
                    }
                }
                else
                {
                    shotCounts[player] = 1;
                }
                if (explosive != null)
                {
                    explosive.damageTypes = new List<DamageTypeEntry>
                {
                    new DamageTypeEntry { amount = 20f, type = DamageType.Explosion }

                };
                    timer.Once(1.7f, () =>
                    {
                        if (explosive != null)
                        {
                            explosive.Explode();
                        }
                    });
                }
            }

        }
        void RemoveM249(BasePlayer player)
        {
            var heldItem = player.GetHeldEntity() as HeldEntity;
            if (heldItem == null) return;
            var item = heldItem.GetItem();
            if (item == null) return;

            item.Remove();
            Effect.server.Run("assets/bundled/prefabs/fx/item_break.prefab", player, 0, Vector3.zero, Vector3.forward);
            shotCounts.Remove(player);
        }
    }
}