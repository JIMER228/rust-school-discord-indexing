using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Rust;
using UnityEngine;
using WebSocketSharp;

namespace Oxide.Plugins
{
    [Info("Winter Clothes", "Hougan", "0.0.1")]
    public class WinterClothes : RustPlugin
    {
        #region Classess

        #endregion

        #region Methods

        private void DestroyAll<T>()
        {
            var objects = UnityEngine.Object.FindObjectsOfType(typeof(T));
            objects?.ToList().ForEach(UnityEngine.Object.Destroy); 
        }

        #endregion
        
        #region Hooks
        
        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (player.TimeAlive() < 60) 
            {
                var list = new List<SleepingBag>();                
                Vis.Entities(player.transform.position, 5f, list);
                if (list.Count > 0) return;
                
                if (TerrainMeta.BiomeMap.GetBiomeMaxType(player.transform.position) == 8)
                {
                    ItemManager.CreateByPartialName("jacket.snow", 1).MoveToContainer(player.inventory.containerWear);   
                    ItemManager.CreateByPartialName("hat.beenie", 1).MoveToContainer(player.inventory.containerWear);   
                    ItemManager.CreateByPartialName("burlap.trousers", 1).MoveToContainer(player.inventory.containerWear);    
                }
            }
        } 

        #endregion
    }
}