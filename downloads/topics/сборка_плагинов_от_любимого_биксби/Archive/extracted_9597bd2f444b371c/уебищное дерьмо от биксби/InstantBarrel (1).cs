using System;
using System.Linq;
using Oxide.Core;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("InstantBarrel", "b1xbyy", "1.0.4")]
    public class InstantBarrel : RustPlugin
    {
        private readonly string[] lootBarrelsNames = 
        { 
            "loot_barrel_1", "loot_barrel_2", "loot-barrel-1", "loot-barrel-2", 
            "oil_barrel", "roadsign1", "roadsign2", "roadsign3", "roadsign4", 
            "roadsign5", "roadsign6", "roadsign7", "roadsign8", "roadsign9" 
        };

        private object OnEntityTakeDamage(LootContainer lootContainer, HitInfo hitInfo)
        {
            if (lootContainer == null || hitInfo == null)
            return null;
            
            var lootContainerName = lootContainer.ShortPrefabName;

            if (lootContainerName == null || !lootBarrelsNames.Contains(lootContainerName))
            return null;
            
            var player = lootContainer.lastAttacker as BasePlayer ?? hitInfo.InitiatorPlayer;

            if (player == null)
            return null;

            var itemContainer = lootContainer?.inventory;

            if (itemContainer == null)
            return null;

            if ((int)Vector3.Distance(player.transform.position, lootContainer.transform.position) > 3f)
            return null;

            if (hitInfo.IsProjectile())
            return null;

            for (int i = itemContainer.itemList.Count - 1; i >= 0; i--)
            player.GiveItem(itemContainer.itemList[i], BaseEntity.GiveItemReason.PickedUp);

            if (itemContainer.itemList == null || itemContainer.itemList.Count <= 0)
            {
                NextTick(() =>
                {
                    Interface.CallHook("OnEntityDeath", lootContainer, hitInfo);
                    lootContainer?.Kill(BaseNetworkable.DestroyMode.Gib);
                });
            }
            return false;
        }
    }
}