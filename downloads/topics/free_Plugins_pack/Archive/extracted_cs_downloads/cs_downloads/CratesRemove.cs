using System;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CratesRemove", "41kg", "1.0.1")]
    [Description("Удаляет недолутанные ящики через 30 сек.")]
    public class CratesRemove : CovalencePlugin
    {
        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (entity == null || player == null) return;
            
            if (entity is StorageContainer storageContainer && storageContainer.inventory?.itemList.Count == 0)
            {
                return;
            }
            
            if (entity.ShortPrefabName.Contains("crate") || entity.ShortPrefabName.Contains("loot"))
            {
                timer.Once(30f, () => RemoveEntity(entity, player));
            }
        }
        
        private void RemoveEntity(BaseEntity entity, BasePlayer player)
        {
            if (entity == null || entity.IsDestroyed) return;
            
            entity.Kill();
        }
    }
}