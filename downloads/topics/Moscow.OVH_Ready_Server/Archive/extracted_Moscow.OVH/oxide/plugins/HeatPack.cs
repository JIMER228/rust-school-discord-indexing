using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Facepunch.Extend;
using Oxide.Core;
using Rust;
using UnityEngine;
using WebSocketSharp;

namespace Oxide.Plugins
{
    [Info("Heat Pack", "Hougan", "0.0.1")]
    public class HeatPack : RustPlugin
    {
        #region Classess

        private class Configuration
        {
            public static float Length = 20;
            public static ulong WaterSkinID = 1832052343;
            public static ulong BottleSkinID = 1832045516;
        }

        #endregion

        #region Methods

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            NextTick(() =>
            {
                if (entity.PrefabName.Contains("vehicle_parts")) 
                {
                    entity.Kill();
                }
            });
        }
 
        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            var obj = entity.GetComponent<StorageContainer>(); 
            if (obj == null || entity.OwnerID != 0 || entity.HasFlag(BaseEntity.Flags.Reserved6)) return;
            
            entity.SetFlag(BaseEntity.Flags.Reserved7, true); 
            timer.Once(0.1f, () =>
            {
                var akComp = obj.inventory.itemList.FirstOrDefault(p => p.skin == Configuration.BottleSkinID);
                if (akComp == null) return;
            
                var item = ItemManager.CreateByPartialName("water", 500, Configuration.WaterSkinID);
                        akComp.contents.Insert(item);  
            });
        }
        
        private object OnItemUse(Item item, int amountToUse)
        {
            if (item.info.shortname != "water") return null; 

            var parent = item.GetRootContainer();
            if (parent == null || parent.playerOwner == null) return null;

            if (item.skin == Configuration.WaterSkinID)
            {
                item.amount -= 100;
                NextTick(() =>
                {
                    if (item.amount == 0)
                    {
                        parent.playerOwner.GetActiveItem().Remove();
                    }
                });
                
                parent.playerOwner.metabolism.pending_health.value += 10;
                parent.playerOwner.metabolism.bleeding.value = 0;
                parent.playerOwner.metabolism.SendChangesToClient();
            }
            return false;
        }

        #endregion
    }
}